using System.Globalization;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

namespace BiogasApp.Business.Pid;

public static class PidDxfWriter
{
    public const string Disclaimer =
        "DRAFT P&ID - AUTO-GENERATED FROM SIZED EQUIPMENT, NOT A DESIGN APPROVAL. VERIFY WITH A QUALIFIED ENGINEER.";

    private const double S = 0.25;
    private const string LayerBox = "BOX";
    private const string LayerValve = "VALVE";
    private const string LayerInstr = "INSTR";
    private const string LayerLeader = "LEADER";
    private const string LayerText = "TEXT";
    private const string LayerTitle = "TITLE";

    private static readonly Dictionary<int, string> StageNames = new()
    {
        [1] = "FEED",
        [2] = "PRE-TREATMENT",
        [3] = "DIGESTION",
        [4] = "GAS HOLDING",
        [5] = "UPGRADING",
        [6] = "CBG"
    };

    private static readonly Dictionary<PidInstrumentType, string> InstrumentNames = new()
    {
        [PidInstrumentType.PT] = "PRESSURE TRANSMITTER",
        [PidInstrumentType.TT] = "TEMPERATURE TRANSMITTER",
        [PidInstrumentType.FT] = "FLOW TRANSMITTER",
        [PidInstrumentType.LT] = "LEVEL TRANSMITTER"
    };

    private sealed class Placer
    {
        private readonly double _cx;
        private readonly double _cy;
        private readonly double _cos;
        private readonly double _sin;

        public Placer(double cx, double cy, double rotationDeg)
        {
            _cx = cx;
            _cy = cy;
            var rad = rotationDeg * Math.PI / 180.0;
            _cos = Math.Cos(rad);
            _sin = Math.Sin(rad);
        }

        public Vector2 Pt(double lx, double ly)
        {
            var rx = lx * _cos - ly * _sin;
            var ry = lx * _sin + ly * _cos;
            return new Vector2((_cx + rx) * S, -(_cy + ry) * S);
        }
    }

    private sealed class Bounds
    {
        public double MinX = double.MaxValue;
        public double MinY = double.MaxValue;
        public double MaxX = double.MinValue;
        public double MaxY = double.MinValue;

        public void Add(double x, double y)
        {
            if (x < MinX) MinX = x;
            if (y < MinY) MinY = y;
            if (x > MaxX) MaxX = x;
            if (y > MaxY) MaxY = y;
        }

        public bool Empty => MinX > MaxX;
    }

    public static byte[] Write(PidDiagramDto d, string projectName, DateTime date)
    {
        if (d.Nodes.Count == 0)
            throw new InvalidOperationException("No equipment to draw in the P&ID.");

        var doc = new DxfDocument();
        doc.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
        CreateLayers(doc);

        var nodes = d.Nodes.ToDictionary(n => n.Id);
        var lines = d.Lines.ToDictionary(l => l.Id);
        var bounds = new Bounds();

        foreach (var l in d.Lines) DrawLine(doc, l, bounds);
        foreach (var n in d.Nodes) DrawNode(doc, n, bounds);
        foreach (var i in d.Instruments) DrawInstrument(doc, i, nodes, lines, bounds);
        foreach (var v in d.Valves) DrawValve(doc, v, bounds);

        DrawStageHeadings(doc, d, bounds);

        var left = bounds.MinX * S;
        var right = bounds.MaxX * S;
        var bottom = -bounds.MaxY * S;
        var top = -bounds.MinY * S;
        var midX = (left + right) / 2.0;

        AddText(doc, LayerText, "PIPING AND INSTRUMENTATION DIAGRAM - NOT TO SCALE", midX, top + 18, 5.0, TextAlignment.MiddleCenter);

        var legendBottom = DrawLegend(doc, d, left, bottom - 14);
        DrawTitleBlock(doc, d, projectName, date, left, legendBottom - 12);

        using var ms = new MemoryStream();
        if (!doc.Save(ms)) throw new InvalidOperationException("DXF save failed.");
        return ms.ToArray();
    }

    private static void CreateLayers(DxfDocument doc)
    {
        void AddLayer(string name, AciColor color, Lineweight weight, Linetype? lt = null)
        {
            var layer = new Layer(name) { Color = color, Lineweight = weight };
            if (lt != null) layer.Linetype = lt;
            doc.Layers.Add(layer);
        }

        AddLayer(LayerBox, new AciColor(7), Lineweight.W70);
        AddLayer(LayerValve, new AciColor(7), Lineweight.W50);
        AddLayer(LayerInstr, new AciColor(7), Lineweight.W35);
        AddLayer(LayerLeader, new AciColor(8), Lineweight.W13, Linetype.Dot);
        AddLayer(LayerText, new AciColor(7), Lineweight.Default);
        AddLayer(LayerTitle, new AciColor(7), Lineweight.Default);

        foreach (PidMedium m in Enum.GetValues(typeof(PidMedium)))
        {
            AddLayer(PipeLayer(m), MediumColor(m), Lineweight.W50, MediumLinetype(m));
        }
    }

    private static string PipeLayer(PidMedium m) => "PIPE_" + m.ToString().ToUpperInvariant();

    private static AciColor MediumColor(PidMedium m) => m switch
    {
        PidMedium.Slurry => new AciColor(139, 90, 43),
        PidMedium.Biogas => new AciColor(217, 130, 43),
        PidMedium.Cbg => new AciColor(47, 158, 68),
        PidMedium.Water => new AciColor(28, 126, 214),
        PidMedium.Digestate => new AciColor(92, 61, 30),
        PidMedium.Steam => new AciColor(134, 142, 150),
        PidMedium.Air => new AciColor(21, 170, 191),
        PidMedium.Condensate => new AciColor(66, 99, 235),
        _ => new AciColor(73, 80, 87)
    };

    private static Linetype MediumLinetype(PidMedium m) => m switch
    {
        PidMedium.Digestate => Linetype.Dashed,
        PidMedium.Steam => Linetype.Dot,
        PidMedium.Air => Linetype.Dashed,
        PidMedium.Condensate => Linetype.DashDot,
        _ => Linetype.Continuous
    };

    private static string MediumLabel(PidMedium m) => m switch
    {
        PidMedium.Cbg => "CBG",
        _ => m.ToString().ToUpperInvariant()
    };

    /* ---------- lines ---------- */

    private static void DrawLine(DxfDocument doc, PidLineDto l, Bounds bounds)
    {
        var pts = l.Points;
        if (pts.Count < 2) return;

        var layer = PipeLayer(l.Medium);
        var verts = pts.Select(p => new Vector2((double)p.X * S, -(double)p.Y * S)).ToList();
        Add(doc, layer, new Polyline2D(verts, false));
        foreach (var p in pts) bounds.Add((double)p.X, (double)p.Y);

        var a = pts[^2];
        var b = pts[^1];
        DrawArrow(doc, layer, (double)a.X, (double)a.Y, (double)b.X, (double)b.Y);

        var bestLen = -1.0;
        var bi = 1;
        for (var i = 1; i < pts.Count; i++)
        {
            var len = Math.Abs((double)(pts[i].X - pts[i - 1].X)) + Math.Abs((double)(pts[i].Y - pts[i - 1].Y));
            if (len > bestLen) { bestLen = len; bi = i; }
        }

        var s0 = pts[bi - 1];
        var s1 = pts[bi];
        var mx = (double)(s0.X + s1.X) / 2.0;
        var my = (double)(s0.Y + s1.Y) / 2.0;
        var horizontal = Math.Abs((double)(s0.Y - s1.Y)) < 0.01;
        var label = $"{l.LineNo}";

        if (horizontal)
        {
            AddText(doc, LayerText, label, mx * S, -(my - 7) * S, 2.2, TextAlignment.MiddleCenter);
        }
        else
        {
            var t = new Text(label, new Vector2((mx + 7) * S, -my * S), 2.2)
            {
                Alignment = TextAlignment.MiddleCenter,
                Rotation = 90
            };
            Add(doc, LayerText, t);
        }
    }

    private static void DrawArrow(DxfDocument doc, string layer, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;

        var ux = dx / len;
        var uy = dy / len;
        var px = -uy;
        var py = ux;
        var cx = bx - ux * 9;
        var cy = by - uy * 9;

        var tri = new List<Vector2>
        {
            new(bx * S, -by * S),
            new((cx + px * 4) * S, -(cy + py * 4) * S),
            new((cx - px * 4) * S, -(cy - py * 4) * S)
        };
        Add(doc, layer, new Polyline2D(tri, true));
    }

    /* ---------- equipment ---------- */

    private static void DrawNode(DxfDocument doc, PidNodeDto n, Bounds bounds)
    {
        var x = (double)n.X;
        var y = (double)n.Y;
        var (w, h) = PidAutoLayoutService.SymbolSize(n.SymbolType);
        var wd = (double)w;
        var hd = (double)h;

        var p = new Placer(x, y, (double)n.RotationDeg);
        DrawSymbol(doc, p, n.SymbolType);

        var tagY = y + hd / 2.0 + 14.0;
        AddText(doc, LayerText, n.Tag ?? string.Empty, x * S, -tagY * S, 3.0, TextAlignment.MiddleCenter);
        if (!string.IsNullOrWhiteSpace(n.Name))
            AddText(doc, LayerText, n.Name.ToUpperInvariant(), x * S, -(tagY + 11) * S, 2.1, TextAlignment.MiddleCenter);

        bounds.Add(x - wd / 2.0, y - hd / 2.0 - 10);
        bounds.Add(x + wd / 2.0, y + hd / 2.0 + 40);
    }

    private static void DrawSymbol(DxfDocument doc, Placer p, PidSymbolType type)
    {
        switch (type)
        {
            case PidSymbolType.Tank:
                Poly(doc, LayerBox, Arc(p, 0, -35, 40, 12, 0, 2 * Math.PI, 32), true);
                Ln(doc, LayerBox, p.Pt(-40, -35), p.Pt(-40, 35));
                Ln(doc, LayerBox, p.Pt(40, -35), p.Pt(40, 35));
                Poly(doc, LayerBox, Arc(p, 0, 35, 40, 12, 0, Math.PI, 24), false);
                break;

            case PidSymbolType.Digester:
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(-60, -45), p.Pt(-60, 55), p.Pt(60, 55), p.Pt(60, -45) }, false);
                Poly(doc, LayerBox, Arc(p, 0, -45, 60, 25, Math.PI, 2 * Math.PI, 32), false);
                Ln(doc, LayerBox, p.Pt(0, -20), p.Pt(0, 40));
                Ln(doc, LayerBox, p.Pt(-22, 30), p.Pt(22, 30));
                Ln(doc, LayerBox, p.Pt(-16, 8), p.Pt(16, 8));
                break;

            case PidSymbolType.Pump:
                Circ(doc, LayerBox, p.Pt(0, 0), 20);
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(-14, -14), p.Pt(20, 0), p.Pt(-14, 14) }, false);
                break;

            case PidSymbolType.Blower:
                Circ(doc, LayerBox, p.Pt(0, 0), 25);
                Circ(doc, LayerBox, p.Pt(0, 0), 4);
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(-14, -14), p.Pt(0, -20), p.Pt(14, -14) }, false);
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(14, 14), p.Pt(0, 20), p.Pt(-14, 14) }, false);
                break;

            case PidSymbolType.Flare:
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(-6, -10), p.Pt(6, -10), p.Pt(6, 40), p.Pt(-6, 40) }, true);
                Poly(doc, LayerBox, new List<Vector2>
                {
                    p.Pt(0, -40), p.Pt(12, -26), p.Pt(10, -14), p.Pt(0, -8), p.Pt(-10, -14), p.Pt(-12, -26)
                }, true);
                Ln(doc, LayerBox, p.Pt(-20, 40), p.Pt(20, 40));
                break;

            case PidSymbolType.GasHolder:
                Poly(doc, LayerBox, new List<Vector2> { p.Pt(-55, 10), p.Pt(55, 10), p.Pt(55, 40), p.Pt(-55, 40) }, true);
                Poly(doc, LayerBox, Arc(p, 0, 10, 55, 55, Math.PI, 2 * Math.PI, 32), false);
                Poly(doc, LayerBox, Arc(p, 0, 10, 40, 34, Math.PI, 2 * Math.PI, 32), false);
                break;

            case PidSymbolType.Instrument:
                Circ(doc, LayerInstr, p.Pt(0, 0), 15);
                Ln(doc, LayerInstr, p.Pt(-15, 0), p.Pt(15, 0));
                break;

            default:
                Ln(doc, LayerBox, p.Pt(-35, -35), p.Pt(35, -35));
                Ln(doc, LayerBox, p.Pt(-35, 35), p.Pt(35, 35));
                Poly(doc, LayerBox, Arc(p, 35, 0, 15, 35, -Math.PI / 2, Math.PI / 2, 24), false);
                Poly(doc, LayerBox, Arc(p, -35, 0, 15, 35, Math.PI / 2, 3 * Math.PI / 2, 24), false);
                break;
        }
    }

    /* ---------- valves ---------- */

    private static void DrawValve(DxfDocument doc, PidValveDto v, Bounds bounds)
    {
        var x = (double)v.X;
        var y = (double)v.Y;
        var rot = (double)v.RotationDeg;
        var p = new Placer(x, y, rot);
        DrawValveSymbol(doc, p, v.ValveType);

        var vertical = Math.Abs(rot % 180.0) > 45.0 && Math.Abs(rot % 180.0) < 135.0;
        var tag = (v.Tag ?? string.Empty) + (v.IsNormallyOpen ? string.Empty : " (NC)");
        if (vertical)
            AddText(doc, LayerText, tag, (x + 16) * S, -(y + 3) * S, 2.2, TextAlignment.MiddleLeft);
        else
            AddText(doc, LayerText, tag, x * S, -(y + 24) * S, 2.2, TextAlignment.MiddleCenter);

        bounds.Add(x - 15, y - 15);
        bounds.Add(x + 15, y + 30);
    }

    private static void DrawValveSymbol(DxfDocument doc, Placer p, PidValveType type)
    {
        void Bowtie() => Poly(doc, LayerValve, new List<Vector2> { p.Pt(-10, -7), p.Pt(10, 7), p.Pt(10, -7), p.Pt(-10, 7) }, true);

        switch (type)
        {
            case PidValveType.Ball:
                Bowtie();
                Circ(doc, LayerValve, p.Pt(0, 0), 3.5);
                break;

            case PidValveType.Butterfly:
                Bowtie();
                Ln(doc, LayerValve, p.Pt(0, -9), p.Pt(0, 9));
                break;

            case PidValveType.Check:
                Bowtie();
                Poly(doc, LayerValve, new List<Vector2> { p.Pt(-4, 0), p.Pt(4, -5), p.Pt(4, 5) }, true);
                break;

            case PidValveType.Globe:
                Bowtie();
                Circ(doc, LayerValve, p.Pt(0, 0), 3.5);
                break;

            case PidValveType.Relief:
                Poly(doc, LayerValve, new List<Vector2> { p.Pt(-10, 8), p.Pt(0, -2), p.Pt(10, 8) }, true);
                Ln(doc, LayerValve, p.Pt(0, -2), p.Pt(0, -12));
                Ln(doc, LayerValve, p.Pt(-5, -12), p.Pt(5, -12));
                break;

            case PidValveType.Control:
                Bowtie();
                Ln(doc, LayerValve, p.Pt(0, 0), p.Pt(0, -10));
                Poly(doc, LayerValve, Arc(p, 0, -10, 7, 6, Math.PI, 2 * Math.PI, 16), true);
                break;

            case PidValveType.Solenoid:
                Bowtie();
                Ln(doc, LayerValve, p.Pt(0, 0), p.Pt(0, -9));
                Poly(doc, LayerValve, new List<Vector2> { p.Pt(-5, -15), p.Pt(5, -15), p.Pt(5, -9), p.Pt(-5, -9) }, true);
                break;

            default:
                Bowtie();
                Ln(doc, LayerValve, p.Pt(0, -8), p.Pt(0, 0));
                Ln(doc, LayerValve, p.Pt(-4, -8), p.Pt(4, -8));
                break;
        }
    }

    /* ---------- instruments ---------- */

    private static void DrawInstrument(
        DxfDocument doc,
        PidInstrumentDto i,
        Dictionary<Guid, PidNodeDto> nodes,
        Dictionary<Guid, PidLineDto> lines,
        Bounds bounds)
    {
        var x = (double)i.X;
        var y = (double)i.Y;

        double? ax = null, ay = null;
        if (i.PidEquipmentNodeId.HasValue && nodes.TryGetValue(i.PidEquipmentNodeId.Value, out var nd))
        {
            var (_, h) = PidAutoLayoutService.SymbolSize(nd.SymbolType);
            ax = (double)nd.X;
            ay = (double)nd.Y - (double)h / 2.0;
        }
        else if (i.PidLineId.HasValue && lines.TryGetValue(i.PidLineId.Value, out var ln) && ln.Points.Count > 1)
        {
            var q = Nearest(ln.Points, x, y);
            ax = q.X;
            ay = q.Y;
        }

        if (ax.HasValue && ay.HasValue)
            Ln(doc, LayerLeader, new Vector2(x * S, -y * S), new Vector2(ax.Value * S, -ay.Value * S));

        DrawInstrumentBubble(doc, x, y, i.InstrumentType, StripPrefix(i.Tag));
        AddText(doc, LayerText, i.Tag ?? string.Empty, x * S, -(y + 28) * S, 2.2, TextAlignment.MiddleCenter);

        bounds.Add(x - 20, y - 20);
        bounds.Add(x + 20, y + 35);
    }

    private static void DrawInstrumentBubble(DxfDocument doc, double x, double y, PidInstrumentType type, string number)
    {
        var p = new Placer(x, y, 0);
        Circ(doc, LayerInstr, p.Pt(0, 0), 15);
        Ln(doc, LayerInstr, p.Pt(-15, 0), p.Pt(15, 0));
        AddText(doc, LayerText, type.ToString(), x * S, -(y - 7) * S, 2.2, TextAlignment.MiddleCenter);
        AddText(doc, LayerText, number, x * S, -(y + 7) * S, 1.9, TextAlignment.MiddleCenter);
    }

    private static string StripPrefix(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        var i = tag.IndexOf('-');
        return i < 0 ? tag : tag[(i + 1)..];
    }

    private static (double X, double Y) Nearest(List<PidPointDto> pts, double px, double py)
    {
        var best = double.MaxValue;
        var bx = (double)pts[0].X;
        var by = (double)pts[0].Y;

        for (var i = 1; i < pts.Count; i++)
        {
            double ax = (double)pts[i - 1].X, ay = (double)pts[i - 1].Y;
            double cx = (double)pts[i].X, cy = (double)pts[i].Y;
            var dx = cx - ax;
            var dy = cy - ay;
            var l2 = dx * dx + dy * dy;
            var t = l2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / l2 : 0.0;
            t = Math.Max(0.0, Math.Min(1.0, t));
            var qx = ax + dx * t;
            var qy = ay + dy * t;
            var d = (px - qx) * (px - qx) + (py - qy) * (py - qy);
            if (d < best)
            {
                best = d;
                bx = qx;
                by = qy;
            }
        }

        return (bx, by);
    }

    /* ---------- headings, legend, title block ---------- */

    private static void DrawStageHeadings(DxfDocument doc, PidDiagramDto d, Bounds bounds)
    {
        var topSvg = d.Nodes.Min(n => (double)n.Y) - 130;
        foreach (var g in d.Nodes.GroupBy(n => n.StageNo).OrderBy(g => g.Key))
        {
            var cx = g.Average(n => (double)n.X);
            var name = StageNames.TryGetValue(g.Key, out var s) ? s : $"STAGE {g.Key}";
            AddText(doc, LayerText, name, cx * S, -topSvg * S, 3.5, TextAlignment.MiddleCenter);
            bounds.Add(cx, topSvg - 10);
        }
    }

    private static double DrawLegend(DxfDocument doc, PidDiagramDto d, double x, double y)
    {
        var cursor = y;

        AddText(doc, LayerText, "LEGEND", x, cursor, 3.0, TextAlignment.MiddleLeft);
        cursor -= 8;

        var mediums = d.Lines.Select(l => l.Medium).Distinct().OrderBy(m => (int)m).ToList();
        var px = x;
        foreach (var m in mediums)
        {
            Ln(doc, PipeLayer(m), new Vector2(px, cursor), new Vector2(px + 14, cursor));
            AddText(doc, LayerText, MediumLabel(m), px + 17, cursor, 2.2, TextAlignment.MiddleLeft);
            px += 55;
        }
        if (mediums.Count > 0) cursor -= 10;

        var valveTypes = d.Valves.Select(v => v.ValveType).Distinct().OrderBy(t => (int)t).ToList();
        px = x + 10;
        foreach (var t in valveTypes)
        {
            var p = new Placer(px / S, -cursor / S, 0);
            DrawValveSymbol(doc, p, t);
            AddText(doc, LayerText, t.ToString().ToUpperInvariant() + " VALVE", px + 9, cursor, 2.2, TextAlignment.MiddleLeft);
            px += 55;
        }
        if (valveTypes.Count > 0) cursor -= 12;

        var instTypes = d.Instruments.Select(i => i.InstrumentType).Distinct().OrderBy(t => (int)t).ToList();
        px = x + 10;
        foreach (var t in instTypes)
        {
            DrawInstrumentBubble(doc, px / S, -cursor / S, t, "000");
            var name = InstrumentNames.TryGetValue(t, out var nm) ? nm : t.ToString();
            AddText(doc, LayerText, name, px + 8, cursor, 2.2, TextAlignment.MiddleLeft);
            px += 70;
        }
        if (instTypes.Count > 0) cursor -= 12;

        return cursor;
    }

    private static void DrawTitleBlock(DxfDocument doc, PidDiagramDto d, string projectName, DateTime date, double x, double top)
    {
        const double width = 170.0;
        const double labelWidth = 40.0;
        const double rowH = 7.0;
        const double titleH = 9.0;

        var rows = new List<(string Label, string Value)>
        {
            ("PROJECT", projectName ?? string.Empty),
            ("DRAWING TITLE", d.Title ?? string.Empty),
            ("DRAWING NO / REV", $"{d.DrawingNo} / R{d.RevisionNo}"),
            ("STATUS", d.Status.ToString().ToUpperInvariant()),
            ("DATE", date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant())
        };

        var total = titleH + rows.Count * rowH + rowH;
        var bottom = top - total;

        Add(doc, LayerTitle, new Polyline2D(new List<Vector2>
        {
            new(x, bottom), new(x + width, bottom), new(x + width, top), new(x, top)
        }, true));

        var cursor = top - titleH;
        AddText(doc, LayerTitle, "PIPING AND INSTRUMENTATION DIAGRAM (P&ID)", x + width / 2, top - titleH / 2, 3.5, TextAlignment.MiddleCenter);
        Ln(doc, LayerTitle, new Vector2(x, cursor), new Vector2(x + width, cursor));

        foreach (var (label, value) in rows)
        {
            var mid = cursor - rowH / 2;
            AddText(doc, LayerTitle, label, x + 2, mid, 2.2, TextAlignment.MiddleLeft);
            AddText(doc, LayerTitle, value, x + labelWidth + 2, mid, 2.2, TextAlignment.MiddleLeft);
            cursor -= rowH;
            Ln(doc, LayerTitle, new Vector2(x, cursor), new Vector2(x + width, cursor));
        }

        Ln(doc, LayerTitle, new Vector2(x + labelWidth, top - titleH), new Vector2(x + labelWidth, cursor));
        AddText(doc, LayerTitle, Disclaimer, x + 2, cursor - rowH / 2, 1.8, TextAlignment.MiddleLeft);
    }

    /* ---------- primitives ---------- */

    private static List<Vector2> Arc(Placer p, double cx, double cy, double rx, double ry, double a0, double a1, int steps)
    {
        var pts = new List<Vector2>();
        var count = a1 - a0 >= 2 * Math.PI - 1e-9 ? steps : steps + 1;
        for (var i = 0; i < count; i++)
        {
            var t = a0 + (a1 - a0) * i / steps;
            pts.Add(p.Pt(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return pts;
    }

    private static void Poly(DxfDocument doc, string layer, List<Vector2> pts, bool closed)
        => Add(doc, layer, new Polyline2D(pts, closed));

    private static void Circ(DxfDocument doc, string layer, Vector2 center, double radiusSvg)
        => Add(doc, layer, new Circle(center, radiusSvg * S));

    private static void Ln(DxfDocument doc, string layer, Vector2 a, Vector2 b)
        => Add(doc, layer, new Line(a, b));

    private static void Add(DxfDocument doc, string layer, EntityObject entity)
    {
        entity.Layer = doc.Layers[layer];
        doc.Entities.Add(entity);
    }

    private static void AddText(DxfDocument doc, string layer, string value, double x, double y,
        double height, TextAlignment alignment)
    {
        var t = new Text(value, new Vector2(x, y), height) { Alignment = alignment };
        Add(doc, layer, t);
    }
}
