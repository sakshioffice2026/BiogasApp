using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class DatasheetExportRepository : IDatasheetExport
{
    private static readonly string[] UtilityKeywords = { "kw", "power", "heat", "duty", "electric", "water", "steam" };

    private readonly IPidRepository _pid;
    private readonly ApplicationDbContext _db;

    public DatasheetExportRepository(IPidRepository pid, ApplicationDbContext db)
    {
        _pid = pid;
        _db = db;
    }

    public async Task<EquipmentDatasheetSetDto> GetDatasheetsAsync(Guid pidDiagramId, CancellationToken cancellationToken = default)
    {
        var d = await _pid.GetByIdAsync(pidDiagramId, cancellationToken)
            ?? throw new InvalidOperationException("P&ID not found.");

        var projectName = await _pid.GetProjectNameAsync(d.ProjectId, cancellationToken) ?? string.Empty;
        var projectItems = await _pid.GetProjectEquipmentAsync(d.ProjectId, cancellationToken);
        var sized = await _pid.GetSizedEquipmentAsync(d.SimulationRunId, cancellationToken);

        var vendorRows = await _db.PidEquipmentVendorData.AsNoTracking()
            .Where(v => v.PidDiagramId == pidDiagramId)
            .ToListAsync(cancellationToken);
        var vendorByTag = vendorRows.ToDictionary(v => v.Tag, v => v, StringComparer.OrdinalIgnoreCase);

        var lineById = d.Lines.ToDictionary(l => l.Id, l => l);

        var set = new EquipmentDatasheetSetDto
        {
            PidDiagramId = d.Id,
            ProjectId = d.ProjectId,
            ProjectName = projectName,
            DrawingNo = d.DrawingNo,
            Title = d.Title,
            RevisionNo = d.RevisionNo,
            Status = d.Status,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var node in d.Nodes.OrderBy(n => n.Tag, StringComparer.OrdinalIgnoreCase))
        {
            var item = node.EquipmentItemId.HasValue
                ? projectItems.FirstOrDefault(p => p.Id == node.EquipmentItemId.Value)
                : projectItems.FirstOrDefault(p => string.Equals(p.Tag, node.Tag, StringComparison.OrdinalIgnoreCase));

            var sz = (node.SizedEquipmentId.HasValue
                    ? sized.FirstOrDefault(s => s.Id == node.SizedEquipmentId.Value)
                    : null)
                ?? sized.FirstOrDefault(s => string.Equals(s.EquipmentTag, node.Tag, StringComparison.OrdinalIgnoreCase))
                ?? SizedByTagPrefix(sized, node.Tag);

            var ds = new EquipmentDatasheetDto
            {
                NodeId = node.Id,
                Tag = node.Tag,
                Name = node.Name,
                SymbolType = node.SymbolType,
                EquipmentType = sz?.EquipmentType ?? node.SymbolType.ToString(),
                Quantity = sz?.Quantity ?? item?.Quantity ?? node.InstanceNo,
                Zone = item?.Zone.ToString(),
                HazardClass = item?.HazardClass.ToString(),
                Shape = item?.Shape.ToString(),
                WidthM = item?.WidthM,
                LengthM = item?.LengthM,
                DiameterM = item?.DiameterM,
                HeightM = ReadHeightM(sz?.GeometryParamsJson),
                NeedsRoadAccess = item?.NeedsRoadAccess
            };

            if (sz != null)
            {
                ds.Mechanical.AddRange(FlattenJson(sz.GeometryParamsJson));

                foreach (var f in FlattenJson(sz.ProcessDutyJson))
                {
                    if (IsUtility(f.Label)) ds.Utilities.Add(f);
                    else ds.Process.Add(f);
                }

                ds.Notes.Add(new DatasheetFieldDto
                {
                    Label = "Basis",
                    Value = "Preliminary values from simulation run; to be confirmed by vendor."
                });
            }
            else
            {
                ds.Notes.Add(new DatasheetFieldDto
                {
                    Label = "Basis",
                    Value = "No sizing data in the simulation run for this tag."
                });
            }

            ds.Instruments = d.Instruments.Where(i => i.PidEquipmentNodeId == node.Id).ToList();

            ds.ConnectedLines = d.Lines
                .Where(l => l.FromNodeId == node.Id || l.ToNodeId == node.Id)
                .OrderBy(l => l.LineNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lineIds = ds.ConnectedLines.Select(l => l.Id).ToHashSet();
            ds.Valves = d.Valves.Where(v => lineIds.Contains(v.PidLineId)).ToList();

            if (vendorByTag.TryGetValue(node.Tag, out var vd))
            {
                ds.Vendor = new EquipmentVendorDataDto
                {
                    Tag = vd.Tag,
                    Make = vd.Make,
                    Model = vd.Model,
                    Material = vd.Material,
                    WeightKg = vd.WeightKg,
                    Price = vd.Price,
                    Currency = vd.Currency,
                    DeliveryWeeks = vd.DeliveryWeeks,
                    Remarks = vd.Remarks
                };
            }
            else
            {
                ds.Vendor = new EquipmentVendorDataDto { Tag = node.Tag };
            }

            set.Items.Add(ds);
        }

        return set;
    }

    public async Task<byte[]> BuildDatasheetExcelAsync(Guid pidDiagramId, string? tag = null, string? view = null, CancellationToken cancellationToken = default)
    {
        var v = NormalizeView(view);
        var set = await GetDatasheetsAsync(pidDiagramId, cancellationToken);

        var items = string.IsNullOrWhiteSpace(tag)
            ? set.Items
            : set.Items.Where(i => string.Equals(i.Tag, tag.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (items.Count == 0)
            throw new InvalidOperationException("No equipment found for this datasheet.");

        using var wb = new XLWorkbook();

        if (string.IsNullOrWhiteSpace(tag))
            BuildIndexSheet(wb.AddWorksheet("Index"), set, items, v);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Index" };
        foreach (var item in items)
        {
            var ws = wb.AddWorksheet(UniqueSheetName(item.Tag, used));
            BuildEquipmentSheet(ws, set, item, v);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> BuildDatasheetPdfAsync(Guid pidDiagramId, string? tag = null, string? view = null, CancellationToken cancellationToken = default)
    {
        var v = NormalizeView(view);
        var set = await GetDatasheetsAsync(pidDiagramId, cancellationToken);

        var items = string.IsNullOrWhiteSpace(tag)
            ? set.Items
            : set.Items.Where(i => string.Equals(i.Tag, tag.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (items.Count == 0)
            throw new InvalidOperationException("No equipment found for this datasheet.");

        return DatasheetPdfWriter.Build(set, items, v, string.IsNullOrWhiteSpace(tag));
    }
    public async Task SaveVendorDataAsync(Guid pidDiagramId, EquipmentVendorDataDto data, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var tag = (data.Tag ?? string.Empty).Trim();
        if (tag.Length == 0)
            throw new InvalidOperationException("Equipment tag is required.");

        var row = await _db.PidEquipmentVendorData
            .FirstOrDefaultAsync(v => v.PidDiagramId == pidDiagramId && v.Tag == tag, cancellationToken);

        if (row == null)
        {
            row = new PidEquipmentVendorData
            {
                Id = Guid.NewGuid(),
                PidDiagramId = pidDiagramId,
                Tag = tag
            };
            _db.PidEquipmentVendorData.Add(row);
        }

        row.Make = Clean(data.Make);
        row.Model = Clean(data.Model);
        row.Material = Clean(data.Material);
        row.WeightKg = data.WeightKg;
        row.Price = data.Price;
        row.Currency = Clean(data.Currency);
        row.DeliveryWeeks = data.DeliveryWeeks;
        row.Remarks = Clean(data.Remarks);
        row.UpdatedBy = updatedBy;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- sheets

    private static void BuildIndexSheet(IXLWorksheet ws, EquipmentDatasheetSetDto set, List<EquipmentDatasheetDto> items, string view)
    {
        const int headerRow = 6;
        var showVendor = view == "epc";

        Text(ws, 1, 1, "EQUIPMENT DATASHEET INDEX");
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        Text(ws, 2, 1, "Project");
        Text(ws, 2, 2, set.ProjectName);
        Text(ws, 3, 1, "Drawing No");
        Text(ws, 3, 2, set.DrawingNo);
        Text(ws, 4, 1, "Revision / Status");
        Text(ws, 4, 2, $"Rev {set.RevisionNo} / {set.Status}");
        ws.Range(2, 1, 4, 1).Style.Font.Bold = true;

        var headers = new List<string>
        {
            "Tag", "Name", "Type", "Qty", "Zone", "Hazard class",
            "Diameter (m)", "Width (m)", "Length (m)", "Height (m)"
        };
        if (showVendor) headers.AddRange(new[] { "Make", "Model", "Material" });

        for (var c = 0; c < headers.Count; c++)
            Text(ws, headerRow, c + 1, headers[c]);

        var head = ws.Range(headerRow, 1, headerRow, headers.Count);
        head.Style.Font.Bold = true;
        head.Style.Font.FontColor = XLColor.White;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");

        var r = headerRow + 1;
        foreach (var i in items)
        {
            Text(ws, r, 1, i.Tag);
            Text(ws, r, 2, i.Name);
            Text(ws, r, 3, i.EquipmentType);
            ws.Cell(r, 4).Value = i.Quantity;
            Text(ws, r, 5, i.Zone);
            Text(ws, r, 6, i.HazardClass);
            Number(ws, r, 7, i.DiameterM);
            Number(ws, r, 8, i.WidthM);
            Number(ws, r, 9, i.LengthM);
            Number(ws, r, 10, i.HeightM);
            if (showVendor)
            {
                Text(ws, r, 11, i.Vendor.Make);
                Text(ws, r, 12, i.Vendor.Model);
                Text(ws, r, 13, i.Vendor.Material);
            }
            r++;
        }

        var table = ws.Range(headerRow, 1, r - 1, headers.Count);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents();
    }

    private static void BuildEquipmentSheet(IXLWorksheet ws, EquipmentDatasheetSetDto set, EquipmentDatasheetDto e, string view)
    {
        var isEpc = view == "epc";
        var isVendor = view == "vendor";

        Text(ws, 1, 1, isVendor ? "EQUIPMENT DATASHEET - VENDOR RFQ" : view == "client" ? "EQUIPMENT DATASHEET - CLIENT" : "EQUIPMENT DATASHEET");
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var r = 2;
        r = KeyValue(ws, r, "Project", set.ProjectName, null);
        r = KeyValue(ws, r, "Drawing No", set.DrawingNo, null);
        r = KeyValue(ws, r, "Revision / Status", $"Rev {set.RevisionNo} / {set.Status}", null);
        r = KeyValue(ws, r, "Tag", e.Tag, null);
        r = KeyValue(ws, r, "Name", e.Name, null);
        r = KeyValue(ws, r, "Type", e.EquipmentType, null);
        r = KeyValue(ws, r, "Quantity", e.Quantity.ToString(CultureInfo.InvariantCulture), null);
        ws.Range(2, 1, r - 1, 1).Style.Font.Bold = true;

        r++;
        r = Section(ws, r, "General and footprint");
        r = KeyValue(ws, r, "Zone", e.Zone, null);
        r = KeyValue(ws, r, "Hazard class", e.HazardClass, null);
        r = KeyValue(ws, r, "Shape", e.Shape, null);
        r = KeyValue(ws, r, "Diameter", Dec(e.DiameterM), "m");
        r = KeyValue(ws, r, "Width", Dec(e.WidthM), "m");
        r = KeyValue(ws, r, "Length", Dec(e.LengthM), "m");
        r = KeyValue(ws, r, "Height", Dec(e.HeightM), "m");
        r = KeyValue(ws, r, "Road access needed", e.NeedsRoadAccess.HasValue ? (e.NeedsRoadAccess.Value ? "Yes" : "No") : null, null);

        r = FieldSection(ws, r, "Process data", e.Process);
        r = FieldSection(ws, r, "Mechanical data", e.Mechanical);

        if (isEpc || isVendor)
        {
            r = FieldSection(ws, r, "Utilities", e.Utilities);

            r = TableSection(ws, r, "Instruments",
                new[] { "Tag", "Type", "Service", "Range min", "Range max", "Unit" },
                e.Instruments.OrderBy(i => i.Tag, StringComparer.OrdinalIgnoreCase).Select(i => new[]
                {
                    i.Tag, i.InstrumentType.ToString(), i.Service,
                    Dec(i.RangeMin), Dec(i.RangeMax), i.Unit
                }).ToList());

            r = TableSection(ws, r, "Connected lines",
                new[] { "Line No", "Medium", "Size DN (mm)", "Material", "Insulated" },
                e.ConnectedLines.Select(l => new[]
                {
                    l.LineNo, l.Medium.ToString(), Dec(l.NominalSizeMm), l.Material, l.IsInsulated ? "Yes" : "No"
                }).ToList());

            var lineNo = e.ConnectedLines.ToDictionary(l => l.Id, l => l.LineNo);
            r = TableSection(ws, r, "Valves on connected lines",
                new[] { "Valve tag", "Type", "Line No", "Size DN (mm)", "Normal position" },
                e.Valves.OrderBy(v => v.Tag, StringComparer.OrdinalIgnoreCase).Select(v => new[]
                {
                    v.Tag, v.ValveType.ToString(), lineNo.GetValueOrDefault(v.PidLineId),
                    Dec(v.NominalSizeMm), v.IsNormallyOpen ? "Open" : "Closed"
                }).ToList());
        }

        if (!isVendor)
            r = FieldSection(ws, r, "Notes", e.Notes);

        if (isEpc)
        {
            r = Section(ws, r, "Vendor data (to be filled)");
            var vendorStart = r;
            r = KeyValue(ws, r, "Make", e.Vendor.Make, null);
            r = KeyValue(ws, r, "Model", e.Vendor.Model, null);
            r = KeyValue(ws, r, "Material", e.Vendor.Material, null);
            r = KeyValue(ws, r, "Weight", Dec(e.Vendor.WeightKg), "kg");
            r = KeyValue(ws, r, "Price", Dec(e.Vendor.Price), e.Vendor.Currency);
            r = KeyValue(ws, r, "Delivery", e.Vendor.DeliveryWeeks?.ToString(CultureInfo.InvariantCulture), "weeks");
            r = KeyValue(ws, r, "Remarks", e.Vendor.Remarks, null);
            ws.Range(vendorStart, 2, r - 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        }
        else if (isVendor)
        {
            r = Section(ws, r, "Vendor data (to be filled by vendor)");
            var vendorStart = r;
            r = KeyValue(ws, r, "Make", null, null);
            r = KeyValue(ws, r, "Model", null, null);
            r = KeyValue(ws, r, "Material", null, null);
            r = KeyValue(ws, r, "Weight", null, "kg");
            r = KeyValue(ws, r, "Price", null, null);
            r = KeyValue(ws, r, "Delivery", null, "weeks");
            ws.Range(vendorStart, 2, r - 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
        }

        ws.Column(1).Width = 28;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 16;
        ws.Column(6).Width = 12;
    }

    // --------------------------------------------------------------- helpers

    private static int Section(IXLWorksheet ws, int row, string title)
    {
        Text(ws, row, 1, title);
        var range = ws.Range(row, 1, row, 6);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        return row + 1;
    }

    private static int KeyValue(IXLWorksheet ws, int row, string label, string? value, string? unit)
    {
        Text(ws, row, 1, label);
        WriteValue(ws, row, 2, value);
        Text(ws, row, 3, unit);
        return row + 1;
    }

    private static int FieldSection(IXLWorksheet ws, int row, string title, List<DatasheetFieldDto> fields)
    {
        if (fields.Count == 0) return row;

        row = Section(ws, row, title);
        foreach (var f in fields)
            row = KeyValue(ws, row, f.Label, f.Value, f.Unit);
        return row + 1;
    }

    private static int TableSection(IXLWorksheet ws, int row, string title, string[] headers, List<string?[]> rows)
    {
        if (rows.Count == 0) return row;

        row = Section(ws, row, title);
        for (var c = 0; c < headers.Length; c++)
            Text(ws, row, c + 1, headers[c]);
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        row++;

        var first = row;
        foreach (var cells in rows)
        {
            for (var c = 0; c < cells.Length; c++)
                WriteValue(ws, row, c + 1, cells[c]);
            row++;
        }

        var table = ws.Range(first - 1, 1, row - 1, headers.Length);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        return row + 1;
    }

    private static void WriteValue(IXLWorksheet ws, int row, int col, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            ws.Cell(row, col).Value = string.Empty;
            return;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            ws.Cell(row, col).Value = number;
        else
            ws.Cell(row, col).Value = value;

        ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private static void Text(IXLWorksheet ws, int row, int col, string? value)
    {
        ws.Cell(row, col).Value = value ?? string.Empty;
    }

    private static void Number(IXLWorksheet ws, int row, int col, decimal? value)
    {
        if (value.HasValue) ws.Cell(row, col).Value = value.Value;
    }

    private static string? Dec(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeView(string? view)
    {
        var v = (view ?? string.Empty).Trim().ToLowerInvariant();
        return v == "client" || v == "vendor" ? v : "epc";
    }
    private static SizedEquipment? SizedByTagPrefix(IEnumerable<SizedEquipment> sized, string? tag)
    {
        var prefix = (tag ?? string.Empty).Split('-')[0].Trim().ToUpperInvariant();
        var sizedTag = prefix switch
        {
            "FT" => "FT-101",
            "MT" => "MIX-101",
            "DT" => "DT-101",
            "AD" => "D-101",
            _ => null
        };

        return sizedTag == null
            ? null
            : sized.FirstOrDefault(s => string.Equals(s.EquipmentTag, sizedTag, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal? ReadHeightM(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            foreach (var key in new[] { "totalHeightM", "heightM", "skidHeightM" })
            {
                if (doc.RootElement.TryGetProperty(key, out var p)
                    && p.ValueKind == JsonValueKind.Number
                    && p.TryGetDecimal(out var v))
                    return v;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool IsUtility(string label)
    {
        var l = label.ToLowerInvariant();
        return UtilityKeywords.Any(k => l.Contains(k));
    }

    private static string UniqueSheetName(string tag, HashSet<string> used)
    {
        var name = Regex.Replace(tag ?? string.Empty, @"[\\/?*\[\]:]", "-").Trim();
        if (name.Length == 0) name = "Equipment";
        if (name.Length > 31) name = name[..31];

        var candidate = name;
        var n = 2;
        while (!used.Add(candidate))
        {
            var suffix = $"-{n++}";
            candidate = (name.Length + suffix.Length > 31 ? name[..(31 - suffix.Length)] : name) + suffix;
        }

        return candidate;
    }

    private static List<DatasheetFieldDto> FlattenJson(string? json)
    {
        var result = new List<DatasheetFieldDto>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                Flatten(doc.RootElement, string.Empty, result);
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static void Flatten(JsonElement element, string prefix, List<DatasheetFieldDto> result)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var label = string.IsNullOrEmpty(prefix) ? Humanize(prop.Name) : $"{prefix} / {Humanize(prop.Name)}";

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(prop.Value, label, result);
                    break;

                case JsonValueKind.Array:
                    var parts = prop.Value.EnumerateArray()
                        .Where(x => x.ValueKind != JsonValueKind.Object && x.ValueKind != JsonValueKind.Array)
                        .Select(Scalar)
                        .ToList();
                    result.Add(new DatasheetFieldDto
                    {
                        Label = label,
                        Value = parts.Count > 0 ? string.Join(", ", parts) : $"{prop.Value.GetArrayLength()} items"
                    });
                    break;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    break;

                default:
                    result.Add(new DatasheetFieldDto { Label = label, Value = Scalar(prop.Value) });
                    break;
            }
        }
    }

    private static string Scalar(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                return e.TryGetDouble(out var d)
                    ? Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture)
                    : e.GetRawText();
            case JsonValueKind.True:
                return "Yes";
            case JsonValueKind.False:
                return "No";
            default:
                return e.GetString() ?? string.Empty;
        }
    }

    private static string Humanize(string key)
    {
        var spaced = Regex.Replace(key.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
        if (spaced.Length == 0) return key;
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}