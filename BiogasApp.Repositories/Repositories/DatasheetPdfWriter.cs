using System.Globalization;
using System.Text;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Repositories;

public static class DatasheetPdfWriter
{
    public static byte[] Build(EquipmentDatasheetSetDto set, List<EquipmentDatasheetDto> items, string? view, bool includeIndex)
    {
        var v = (view ?? string.Empty).Trim().ToLowerInvariant();
        if (v != "client" && v != "vendor") v = "epc";

        var isEpc = v == "epc";
        var isVendor = v == "vendor";
        var viewLabel = isVendor ? "Vendor RFQ" : v == "client" ? "Client" : "EPC";

        var pdf = new Pdf($"BiogasApp - Equipment Datasheet ({viewLabel}) - {set.DrawingNo} Rev {set.RevisionNo}");
        var first = true;

        if (includeIndex)
        {
            pdf.Banner("EQUIPMENT DATASHEET INDEX", viewLabel);
            pdf.InfoRows(new (string, string)[]
            {
                ("Project", S(set.ProjectName)),
                ("Drawing No", S(set.DrawingNo)),
                ("Revision / Status", $"Rev {set.RevisionNo} / {set.Status}")
            });

            var headers = new[] { "Tag", "Name", "Type", "Qty", "Zone", "Hazard", "Dia", "Wid", "Len", "Hgt" };
            var widths = new double[] { 50, 98, 104, 26, 44, 56, 36, 36, 35, 30 };

            var rows = new List<string[]>();
            foreach (var i in items)
            {
                var row = new[]
{
                    S(i.Tag), S(i.Name), Humanize(i.EquipmentType),
                    i.Quantity.ToString(CultureInfo.InvariantCulture),
                    Humanize(i.Zone), Humanize(i.HazardClass),
                    Dec(i.DiameterM), Dec(i.WidthM), Dec(i.LengthM), Dec(i.HeightM)
                };
                rows.Add(row);
            }

            pdf.Section("Equipment list (Dia, Wid, Len, Hgt in m)");
            pdf.Table(headers, widths, rows);
            first = false;
        }

        foreach (var e in items)
        {
            if (!first) pdf.NewPage();
            first = false;

            pdf.Banner($"{S(e.Tag)} - {S(e.Name)}", $"EQUIPMENT DATASHEET - {viewLabel.ToUpperInvariant()}");
            pdf.InfoRows(new (string, string)[]
            {
                ("Project", S(set.ProjectName)),
                ("Drawing No", S(set.DrawingNo)),
                ("Revision / Status", $"Rev {set.RevisionNo} / {set.Status}"),
                ("Type", S(e.EquipmentType)),
                ("Quantity", e.Quantity.ToString(CultureInfo.InvariantCulture))
            });

            pdf.Section("General and footprint");
            pdf.KeyValueRows(new (string, string)[]
            {
                ("Zone", Show(e.Zone, null)),
                ("Hazard class", Show(e.HazardClass, null)),
                ("Shape", Show(e.Shape, null)),
                ("Diameter", Show(Dec(e.DiameterM), "m")),
                ("Width", Show(Dec(e.WidthM), "m")),
                ("Length", Show(Dec(e.LengthM), "m")),
                ("Height", Show(Dec(e.HeightM), "m")),
                ("Road access needed", e.NeedsRoadAccess.HasValue ? (e.NeedsRoadAccess.Value ? "Yes" : "No") : "-")
            });

            FieldSection(pdf, "Process data", e.Process);
            FieldSection(pdf, "Mechanical data", e.Mechanical);

            if (isEpc || isVendor)
            {
                FieldSection(pdf, "Utilities", e.Utilities);

                if (e.Instruments.Count > 0)
                {
                    pdf.Section("Instruments");
                    pdf.Table(
                        new[] { "Tag", "Type", "Service", "Range", "Unit" },
                        new double[] { 80, 90, 150, 125, 70 },
                        e.Instruments.OrderBy(i => i.Tag, StringComparer.OrdinalIgnoreCase).Select(i => new[]
                        {
                            S(i.Tag), i.InstrumentType.ToString(), S(i.Service),
                            $"{Dash(Dec(i.RangeMin))} - {Dash(Dec(i.RangeMax))}", S(i.Unit)
                        }).ToList());
                }

                if (e.ConnectedLines.Count > 0)
                {
                    pdf.Section("Connected lines");
                    pdf.Table(
                        new[] { "Line No", "Medium", "Size DN (mm)", "Material", "Insulated" },
                        new double[] { 100, 120, 90, 125, 80 },
                        e.ConnectedLines.Select(l => new[]
                        {
                            S(l.LineNo), l.Medium.ToString(), Dash(Dec(l.NominalSizeMm)), S(l.Material), l.IsInsulated ? "Yes" : "No"
                        }).ToList());
                }

                if (e.Valves.Count > 0)
                {
                    var lineNo = e.ConnectedLines.ToDictionary(l => l.Id, l => l.LineNo);
                    pdf.Section("Valves on connected lines");
                    pdf.Table(
                        new[] { "Valve tag", "Type", "Line No", "Size DN (mm)", "Normal position" },
                        new double[] { 100, 130, 110, 90, 85 },
                        e.Valves.OrderBy(x => x.Tag, StringComparer.OrdinalIgnoreCase).Select(x => new[]
                        {
                            S(x.Tag), x.ValveType.ToString(), S(lineNo.GetValueOrDefault(x.PidLineId)),
                            Dash(Dec(x.NominalSizeMm)), x.IsNormallyOpen ? "Open" : "Closed"
                        }).ToList());
                }
            }

            if (!isVendor)
                FieldSection(pdf, "Notes", e.Notes);

            if (isEpc)
            {
                pdf.Section("Vendor data");
                pdf.KeyValueRows(new (string, string)[]
                {
                    ("Make", Show(e.Vendor.Make, null)),
                    ("Model", Show(e.Vendor.Model, null)),
                    ("Material", Show(e.Vendor.Material, null)),
                    ("Weight", Show(Dec(e.Vendor.WeightKg), "kg")),
                    ("Price", Show(Dec(e.Vendor.Price), e.Vendor.Currency)),
                    ("Delivery", Show(e.Vendor.DeliveryWeeks?.ToString(CultureInfo.InvariantCulture), "weeks")),
                    ("Remarks", Show(e.Vendor.Remarks, null))
                });
            }
            else if (isVendor)
            {
                pdf.Section("Vendor data (to be filled by vendor)");
                pdf.BlankRows(new[] { "Make", "Model", "Material", "Weight (kg)", "Price", "Delivery (weeks)" });
            }
        }

        return pdf.Build();
    }

    private static void FieldSection(Pdf pdf, string title, List<DatasheetFieldDto> fields)
    {
        if (fields.Count == 0) return;
        pdf.Section(title);
        pdf.KeyValueRows(fields.Select(f => (S(f.Label), Show(f.Value, f.Unit))));
    }

    private static string S(string? value) => value ?? string.Empty;

    private static string Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = System.Text.RegularExpressions.Regex
            .Replace(value.Replace('_', ' '), "([a-z0-9])([A-Z])", "$1 $2")
            .ToLowerInvariant();
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static string Show(string? value, string? unit)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return string.IsNullOrWhiteSpace(unit) ? value : $"{value} {unit}";
    }

    private static string Dec(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
    }

    // ------------------------------------------------------------------ PDF

    private sealed class Pdf
    {
        private const double PageW = 595;
        private const double PageH = 842;
        private const double Margin = 40;
        private const double ContentW = PageW - 2 * Margin;
        private const double BottomLimit = 42;
        private const double ValueX = Margin + 190;

        private const string Teal = "0.059 0.463 0.431";
        private const string Ink = "0.059 0.090 0.165";
        private const string Muted = "0.392 0.455 0.545";
        private const string Rule = "0.886 0.910 0.941";
        private const string Soft = "0.945 0.961 0.976";
        private const string White = "1 1 1";
        private const string Fillable = "1 0.949 0.8";

        private static readonly int[] Widths =
        {
            278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
            556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
            278, 278, 584, 584, 584, 556, 1015,
            667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833,
            722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611,
            278, 278, 278, 469, 556, 333,
            556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833,
            556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500,
            334, 260, 334, 584
        };

        private readonly string _footer;
        private readonly List<StringBuilder> _pages = new();
        private StringBuilder _page = null!;
        private double _y;

        public Pdf(string footer)
        {
            _footer = footer;
            NewPage();
        }

        public void NewPage()
        {
            _page = new StringBuilder();
            _pages.Add(_page);
            _y = PageH - Margin;
        }

        public void Banner(string title, string subtitle)
        {
            DrawRect(0, PageH - 84, PageW, 84, Teal);
            DrawText(Margin, PageH - 40, Fit(title, 18, ContentW), 18, true, White);
            DrawText(Margin, PageH - 62, Fit(subtitle, 10.5, ContentW), 10.5, false, White);
            _y = PageH - 84 - 28;
        }

        public void InfoRows(IEnumerable<(string Label, string Value)> rows)
        {
            foreach (var (label, value) in rows)
            {
                EnsureSpace(16);
                DrawText(Margin, _y, label, 9.5, false, Muted);
                DrawText(Margin + 120, _y, Fit(value, 9.5, ContentW - 120), 9.5, true, Ink);
                _y -= 16;
            }
            _y -= 6;
        }

        public void Section(string title)
        {
            EnsureSpace(60);
            DrawText(Margin, _y, title.ToUpperInvariant(), 10, true, Teal);
            DrawLine(Margin, PageW - Margin, _y - 6, Teal, 0.8);
            _y -= 22;
        }

        public void KeyValueRows(IEnumerable<(string Label, string Value)> rows)
        {
            foreach (var (label, value) in rows)
            {
                EnsureSpace(18);
                DrawText(Margin + 4, _y - 2, Fit(label, 9.5, ValueX - Margin - 12), 9.5, false, Ink);
                DrawText(ValueX, _y - 2, Fit(value, 9.5, PageW - Margin - ValueX - 4), 9.5, true, Ink);
                DrawLine(Margin, PageW - Margin, _y - 8, Rule, 0.5);
                _y -= 18;
            }
            _y -= 8;
        }

        public void BlankRows(IEnumerable<string> labels)
        {
            foreach (var label in labels)
            {
                EnsureSpace(24);
                DrawRect(ValueX, _y - 10, PageW - Margin - ValueX, 20, Fillable);
                DrawText(Margin + 4, _y - 2, label, 9.5, false, Ink);
                DrawLine(Margin, PageW - Margin, _y - 12, Rule, 0.5);
                _y -= 24;
            }
            _y -= 8;
        }

        public void Table(string[] headers, double[] widths, IReadOnlyList<string[]> rows)
        {
            void Header()
            {
                DrawRect(Margin, _y - 14, ContentW, 20, Soft);
                var hx = Margin;
                for (var c = 0; c < headers.Length; c++)
                {
                    DrawText(hx + 6, _y - 7, Fit(headers[c], 8.5, widths[c] - 8), 8.5, true, Muted);
                    hx += widths[c];
                }
                _y -= 26;
            }

            EnsureSpace(70);
            Header();

            foreach (var row in rows)
            {
                if (_y - 18 < BottomLimit)
                {
                    NewPage();
                    Header();
                }

                var rx = Margin;
                for (var c = 0; c < row.Length; c++)
                {
                    DrawText(rx + 6, _y - 2, Fit(row[c] ?? string.Empty, 9, widths[c] - 8), 9, false, Ink);
                    rx += widths[c];
                }
                DrawLine(Margin, PageW - Margin, _y - 8, Rule, 0.5);
                _y -= 18;
            }

            _y -= 8;
        }

        public byte[] Build()
        {
            var total = _pages.Count;
            for (var i = 0; i < total; i++)
            {
                _page = _pages[i];
                DrawLine(Margin, PageW - Margin, 38, Rule, 0.5);
                DrawText(Margin, 24, Fit(_footer, 8, ContentW - 80), 8, false, Muted);
                DrawTextRight(PageW - Margin, 24, $"Page {i + 1} of {total}", 8, false, Muted);
            }

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
            };

            var kids = new List<string>();
            for (var i = 0; i < total; i++)
            {
                var pageId = 5 + i * 2;
                var contentId = pageId + 1;
                kids.Add($"{pageId} 0 R");

                var stream = _pages[i].ToString();
                objects.Add(
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentId} 0 R " +
                    "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> >>");
                objects.Add($"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream");
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", kids)}] /Count {total} >>";

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xrefPosition = pdf.Length;
            pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                pdf.Append($"{offset:D10} 00000 n \n");

            pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");

            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        // ---------------------------------------------------------- helpers

        private void EnsureSpace(double needed)
        {
            if (_y - needed < BottomLimit)
                NewPage();
        }

        private void DrawText(double x, double y, string text, double size, bool bold, string color)
        {
            _page.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(F(size)).Append(" Tf ")
                .Append(color).Append(" rg ").Append(F(x)).Append(' ').Append(F(y)).Append(" Td (")
                .Append(Escape(text)).Append(") Tj ET\n");
        }

        private void DrawTextRight(double xRight, double y, string text, double size, bool bold, string color)
        {
            DrawText(xRight - Measure(text, size), y, text, size, bold, color);
        }

        private void DrawRect(double x, double y, double w, double h, string color)
        {
            _page.Append(color).Append(" rg ")
                .Append(F(x)).Append(' ').Append(F(y)).Append(' ')
                .Append(F(w)).Append(' ').Append(F(h)).Append(" re f\n");
        }

        private void DrawLine(double x1, double x2, double y, string color, double width)
        {
            _page.Append(color).Append(" RG ").Append(F(width)).Append(" w ")
                .Append(F(x1)).Append(' ').Append(F(y)).Append(" m ")
                .Append(F(x2)).Append(' ').Append(F(y)).Append(" l S\n");
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static double Measure(string text, double size)
        {
            double total = 0;
            foreach (var ch in text)
            {
                var index = ch >= ' ' && ch <= '~' ? ch - 32 : '?' - 32;
                total += Widths[index];
            }
            return total * size / 1000.0;
        }

        private static string Fit(string text, double size, double maxWidth)
        {
            if (Measure(text, size) <= maxWidth) return text;

            var cut = text;
            while (cut.Length > 1 && Measure(cut + "...", size) > maxWidth)
                cut = cut[..^1];
            return cut + "...";
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch is '\\' or '(' or ')')
                    sb.Append('\\').Append(ch);
                else if (ch < 32 || ch > 126)
                    sb.Append('?');
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
