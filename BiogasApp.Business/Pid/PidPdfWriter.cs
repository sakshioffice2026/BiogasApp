using System.Globalization;
using System.Text;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Business.Pid;

public static class PidPdfWriter
{
    public const string Disclaimer =
        "DRAFT P&ID - AUTO-GENERATED FROM SIZED EQUIPMENT, NOT A DESIGN APPROVAL. VERIFY WITH A QUALIFIED ENGINEER.";

    private const double PageW = 1190.55;
    private const double PageH = 841.89;
    private const double Margin = 28.0;
    private const double BottomStrip = 118.0;
    private const double TitleW = 330.0;

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

    private static readonly double[] Helv =
    {
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
        278, 278, 584, 584, 584, 556, 1015,
        667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611,
        278, 278, 278, 469, 556, 333,
        556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500,
        334, 260, 334, 584
    };

    private sealed class Medium
    {
        public double R, G, B;
        public double[]? Dash;
        public Medium(double r, double g, double b, double[]? dash = null) { R = r; G = g; B = b; Dash = dash; }
    }

    private static Medium MediumOf(PidMedium m) => m switch
    {
        PidMedium.Slurry => new Medium(0.545, 0.353, 0.169),
        PidMedium.Biogas => new Medium(0.851, 0.510, 0.169),
        PidMedium.Cbg => new Medium(0.184, 0.620, 0.267),
        PidMedium.Water => new Medium(0.110, 0.494, 0.839),
        PidMedium.Digestate => new Medium(0.361, 0.239, 0.118, new[] { 6.0, 3.0 }),
        PidMedium.Steam => new Medium(0.525, 0.557, 0.588, new[] { 1.5, 2.5 }),
        PidMedium.Air => new Medium(0.082, 0.667, 0.749, new[] { 5.0, 3.0 }),
        PidMedium.Condensate => new Medium(0.259, 0.388, 0.922, new[] { 6.0, 2.0, 1.5, 2.0 }),
        _ => new Medium(0.286, 0.314, 0.341)
    };

    private static string MediumLabel(PidMedium m) => m == PidMedium.Cbg ? "CBG" : m.ToString().ToUpperInvariant();

    /* ---------- canvas ---------- */

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Ascii(string? s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s ?? string.Empty)
            sb.Append(ch < 32 || ch > 126 ? '?' : ch);
        return sb.ToString();
    }

    private static string Esc(string s)
        => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static double TextWidth(string s, double size, bool bold)
    {
        double w = 0;
        foreach (var ch in s)
        {
            var i = ch - 32;
            w += i >= 0 && i < Helv.Length ? Helv[i] : 556;
        }
        return w / 1000.0 * size * (bold ? 1.05 : 1.0);
    }

    private sealed class Canvas
    {
        private readonly StringBuilder _sb = new();

        public override string ToString() => _sb.ToString();

        public void Stroke(double r, double g, double b) => _sb.Append($"{N(r)} {N(g)} {N(b)} RG\n");
        public void Fill(double r, double g, double b) => _sb.Append($"{N(r)} {N(g)} {N(b)} rg\n");
        public void Width(double w) => _sb.Append($"{N(w)} w\n");

        public void Dash(double[]? pattern)
        {
            if (pattern == null || pattern.Length == 0) _sb.Append("[] 0 d\n");
            else _sb.Append('[').Append(string.Join(" ", pattern.Select(N))).Append("] 0 d\n");
        }

        public void Poly(List<(double X, double Y)> pts, bool closed, bool fill)
        {
            if (pts.Count < 2) return;
            _sb.Append($"{N(pts[0].X)} {N(pts[0].Y)} m\n");
            for (var i = 1; i < pts.Count; i++)
                _sb.Append($"{N(pts[i].X)} {N(pts[i].Y)} l\n");
            _sb.Append(closed ? (fill ? "b\n" : "s\n") : "S\n");
        }

        public void Line(double x1, double y1, double x2, double y2)
            => _sb.Append($"{N(x1)} {N(y1)} m\n{N(x2)} {N(y2)} l\nS\n");

        public void Circle(double cx, double cy, double r, bool fill)
        {
            const double c = 0.5522847498;
            var k = r * c;
            _sb.Append($"{N(cx + r)} {N(cy)} m\n");
            _sb.Append($"{N(cx + r)} {N(cy + k)} {N(cx + k)} {N(cy + r)} {N(cx)} {N(cy + r)} c\n");
            _sb.Append($"{N(cx - k)} {N(cy + r)} {N(cx - r)} {N(cy + k)} {N(cx - r)} {N(cy)} c\n");
            _sb.Append($"{N(cx - r)} {N(cy - k)} {N(cx - k)} {N(cy - r)} {N(cx)} {N(cy - r)} c\n");
            _sb.Append($"{N(cx + k)} {N(cy - r)} {N(cx + r)} {N(cy - k)} {N(cx + r)} {N(cy)} c\n");
            _sb.Append(fill ? "B\n" : "S\n");
        }

        public void Text(string? value, double x, double y, double size, char align, bool bold, bool vertical = false)
        {
            var s = Ascii(value);
            if (s.Length == 0) return;
            var w = TextWidth(s, size, bold);
            var font = bold ? "F2" : "F1";

            _sb.Append("0.06 0.09 0.13 rg\nBT\n");
            _sb.Append($"/{font} {N(size)} Tf\n");

            if (vertical)
            {
                var start = align == 'c' ? y - w / 2 : (align == 'r' ? y - w : y);
                _sb.Append($"0 1 -1 0 {N(x + size * 0.35)} {N(start)} Tm\n");
            }
            else
            {
                var start = align == 'c' ? x - w / 2 : (align == 'r' ? x - w : x);
                _sb.Append($"1 0 0 1 {N(start)} {N(y - size * 0.35)} Tm\n");
            }

            _sb.Append($"({Esc(s)}) Tj\nET\n");
        }
    }

    private sealed class Placer
    {
        private readonly double _x, _y, _cos, _sin;
        public double K { get; }

        public Placer(double px, double py, double rotDeg, double k)
        {
            _x = px;
            _y = py;
            K = k;
            var rad = rotDeg * Math.PI / 180.0;
            _cos = Math.Cos(rad);
            _sin = Math.Sin(rad);
        }

        public (double X, double Y) Pt(double lx, double ly)
        {
            var rx = lx * _cos - ly * _sin;
            var ry = lx * _sin + ly * _cos;
            return (_x + rx * K, _y - ry * K);
        }
    }

    private sealed class Map
    {
        public double MinX, MinY, Ox, Oy, K;
        public (double X, double Y) Pg(double sx, double sy) => (Ox + (sx - MinX) * K, Oy - (sy - MinY) * K);
    }

    private sealed class Bounds
    {
        public double MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue;

        public void Add(double x, double y)
        {
            if (x < MinX) MinX = x;
            if (y < MinY) MinY = y;
            if (x > MaxX) MaxX = x;
            if (y > MaxY) MaxY = y;
        }
    }

    /* ---------- entry ---------- */

    public static byte[] Write(PidDiagramDto d, string projectName, DateTime date)
    {
        if (d.Nodes.Count == 0)
            throw new InvalidOperationException("No equipment to draw in the P&ID.");

        var nodes = d.Nodes.ToDictionary(n => n.Id);
        var lines = d.Lines.ToDictionary(l => l.Id);

        var headingY = d.Nodes.Min(n => (double)n.Y) - 130;
        if (d.Instruments.Count > 0)
            headingY = Math.Min(headingY, d.Instruments.Min(i => (double)i.Y) - 45);
        var b = new Bounds();
        foreach (var n in d.Nodes)
        {
            var (w, h) = PidAutoLayoutService.SymbolSize(n.SymbolType);
            b.Add((double)n.X - (double)w / 2, (double)n.Y - (double)h / 2 - 10);
            b.Add((double)n.X + (double)w / 2, (double)n.Y + (double)h / 2 + 40);
        }
        foreach (var l in d.Lines) foreach (var p in l.Points) b.Add((double)p.X, (double)p.Y);
        foreach (var v in d.Valves) { b.Add((double)v.X - 15, (double)v.Y - 15); b.Add((double)v.X + 15, (double)v.Y + 30); }
        foreach (var i in d.Instruments) { b.Add((double)i.X - 20, (double)i.Y - 20); b.Add((double)i.X + 20, (double)i.Y + 35); }
        b.Add(b.MinX, headingY - 10);

        var rx0 = Margin + 10;
        var rx1 = PageW - Margin - 10;
        var ry0 = Margin + BottomStrip + 8;
        var ry1 = PageH - Margin - 34;
        var bw = Math.Max(1, b.MaxX - b.MinX);
        var bh = Math.Max(1, b.MaxY - b.MinY);
        var k = Math.Min(Math.Min((rx1 - rx0) / bw, (ry1 - ry0) / bh), 1.2);

        var map = new Map
        {
            K = k,
            MinX = b.MinX,
            MinY = b.MinY,
            Ox = rx0 + ((rx1 - rx0) - bw * k) / 2,
            Oy = ry1 - ((ry1 - ry0) - bh * k) / 2
        };

        var c = new Canvas();

        c.Stroke(0.12, 0.16, 0.2);
        c.Width(1.2);
        c.Poly(new List<(double, double)>
        {
            (Margin, Margin), (PageW - Margin, Margin), (PageW - Margin, PageH - Margin), (Margin, PageH - Margin)
        }, true, false);

        c.Text("PIPING AND INSTRUMENTATION DIAGRAM - NOT TO SCALE", PageW / 2, PageH - Margin - 16, 13, 'c', true);

        foreach (var g in d.Nodes.GroupBy(n => n.StageNo).OrderBy(g => g.Key))
        {
            var cx = g.Average(n => (double)n.X);
            var pos = map.Pg(cx, headingY);
            var name = StageNames.TryGetValue(g.Key, out var sn) ? sn : $"STAGE {g.Key}";
            c.Text(name, pos.X, pos.Y, Math.Max(6.5, 15 * k), 'c', true);
        }

        foreach (var l in d.Lines) DrawLine(c, map, l);
        foreach (var n in d.Nodes) DrawNode(c, map, n);
        foreach (var i in d.Instruments) DrawInstrument(c, map, i, nodes, lines);
        foreach (var v in d.Valves) DrawValve(c, map, v);

        DrawLegend(c, d, Margin + 12, Margin + BottomStrip - 12);
        DrawTitleBlock(c, d, projectName, date);

        return Assemble(c.ToString());
    }

    /* ---------- drawing ---------- */

    private static double Fs(double svgSize, double k) => Math.Max(4.5, svgSize * k);

    private static void DrawLine(Canvas c, Map map, PidLineDto l)
    {
        var pts = l.Points;
        if (pts.Count < 2) return;

        var m = MediumOf(l.Medium);
        var pg = pts.Select(p => map.Pg((double)p.X, (double)p.Y)).ToList();

        c.Stroke(m.R, m.G, m.B);
        c.Width(l.IsInsulated ? 2.2 : 1.3);
        c.Dash(m.Dash);
        c.Poly(pg, false, false);
        c.Dash(null);

        var a = pg[^2];
        var e = pg[^1];
        var dx = e.X - a.X;
        var dy = e.Y - a.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1e-6)
        {
            var ux = dx / len;
            var uy = dy / len;
            var px = -uy;
            var py = ux;
            var s = Math.Max(5.0, 9 * map.K);
            var bx = e.X - ux * s;
            var by = e.Y - uy * s;
            c.Fill(m.R, m.G, m.B);
            c.Width(0.5);
            c.Poly(new List<(double, double)>
            {
                (e.X, e.Y), (bx + px * s * 0.45, by + py * s * 0.45), (bx - px * s * 0.45, by - py * s * 0.45)
            }, true, true);
        }

        var best = -1.0;
        var bi = 1;
        for (var i = 1; i < pg.Count; i++)
        {
            var sl = Math.Abs(pg[i].X - pg[i - 1].X) + Math.Abs(pg[i].Y - pg[i - 1].Y);
            if (sl > best) { best = sl; bi = i; }
        }

        var mx = (pg[bi - 1].X + pg[bi].X) / 2;
        var my = (pg[bi - 1].Y + pg[bi].Y) / 2;
        var horizontal = Math.Abs(pg[bi - 1].Y - pg[bi].Y) < 0.01;
        var size = Fs(9, map.K);

        if (horizontal) c.Text(l.LineNo, mx, my + 7 * map.K + size * 0.4, size, 'c', false);
        else c.Text(l.LineNo, mx + 7 * map.K + size * 0.4, my, size, 'c', false, true);
    }

    private static void DrawNode(Canvas c, Map map, PidNodeDto n)
    {
        var pos = map.Pg((double)n.X, (double)n.Y);
        var p = new Placer(pos.X, pos.Y, (double)n.RotationDeg, map.K);

        c.Stroke(0.12, 0.16, 0.2);
        c.Fill(1, 1, 1);
        c.Width(1.0);
        c.Dash(null);
        DrawSymbol(c, p, n.SymbolType);

        var (_, h) = PidAutoLayoutService.SymbolSize(n.SymbolType);
        var tagOffset = (double)h / 2 + 14;
        c.Text(n.Tag, pos.X, pos.Y - tagOffset * map.K, Fs(12, map.K), 'c', true);
        if (!string.IsNullOrWhiteSpace(n.Name))
            c.Text(n.Name.ToUpperInvariant(), pos.X, pos.Y - (tagOffset + 11) * map.K, Fs(8.5, map.K), 'c', false);
    }

    private static List<(double X, double Y)> Arc(Placer p, double cx, double cy, double rx, double ry, double a0, double a1, int steps)
    {
        var pts = new List<(double X, double Y)>();
        var full = a1 - a0 >= 2 * Math.PI - 1e-9;
        var count = full ? steps : steps + 1;
        for (var i = 0; i < count; i++)
        {
            var t = a0 + (a1 - a0) * i / steps;
            pts.Add(p.Pt(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return pts;
    }

    private static void DrawSymbol(Canvas c, Placer p, PidSymbolType type)
    {
        List<(double X, double Y)> L(params (double, double)[] pts) => pts.Select(q => p.Pt(q.Item1, q.Item2)).ToList();
        void Ln((double X, double Y) a, (double X, double Y) b) => c.Line(a.X, a.Y, b.X, b.Y);

        switch (type)
        {
            case PidSymbolType.Tank:
                c.Poly(Arc(p, 0, -35, 40, 12, 0, 2 * Math.PI, 32), true, true);
                Ln(p.Pt(-40, -35), p.Pt(-40, 35));
                Ln(p.Pt(40, -35), p.Pt(40, 35));
                c.Poly(Arc(p, 0, 35, 40, 12, 0, Math.PI, 24), false, false);
                break;

            case PidSymbolType.Digester:
                c.Poly(L((-60, -45), (-60, 55), (60, 55), (60, -45)), false, false);
                c.Poly(Arc(p, 0, -45, 60, 25, Math.PI, 2 * Math.PI, 32), false, false);
                Ln(p.Pt(0, -20), p.Pt(0, 40));
                Ln(p.Pt(-22, 30), p.Pt(22, 30));
                Ln(p.Pt(-16, 8), p.Pt(16, 8));
                break;

            case PidSymbolType.Pump:
                var pc = p.Pt(0, 0);
                c.Circle(pc.X, pc.Y, 20 * p.K, true);
                c.Poly(L((-14, -14), (20, 0), (-14, 14)), false, false);
                break;

            case PidSymbolType.Blower:
                var bc = p.Pt(0, 0);
                c.Circle(bc.X, bc.Y, 25 * p.K, true);
                c.Circle(bc.X, bc.Y, 4 * p.K, false);
                c.Poly(L((-14, -14), (0, -20), (14, -14)), false, false);
                c.Poly(L((14, 14), (0, 20), (-14, 14)), false, false);
                break;

            case PidSymbolType.Flare:
                c.Poly(L((-6, -10), (6, -10), (6, 40), (-6, 40)), true, true);
                c.Poly(L((0, -40), (12, -26), (10, -14), (0, -8), (-10, -14), (-12, -26)), true, true);
                Ln(p.Pt(-20, 40), p.Pt(20, 40));
                break;

            case PidSymbolType.GasHolder:
                c.Poly(L((-55, 10), (55, 10), (55, 40), (-55, 40)), true, true);
                c.Poly(Arc(p, 0, 10, 55, 55, Math.PI, 2 * Math.PI, 32), true, true);
                c.Poly(Arc(p, 0, 10, 40, 34, Math.PI, 2 * Math.PI, 32), false, false);
                break;

            case PidSymbolType.Instrument:
                var ic = p.Pt(0, 0);
                c.Circle(ic.X, ic.Y, 15 * p.K, true);
                Ln(p.Pt(-15, 0), p.Pt(15, 0));
                break;

            default:
                var body = new List<(double X, double Y)>();
                body.AddRange(L((-35, -35), (35, -35)));
                body.AddRange(Arc(p, 35, 0, 15, 35, -Math.PI / 2, Math.PI / 2, 24));
                body.AddRange(L((35, 35), (-35, 35)));
                body.AddRange(Arc(p, -35, 0, 15, 35, Math.PI / 2, 3 * Math.PI / 2, 24));
                c.Poly(body, true, true);
                break;
        }
    }

    private static void DrawValve(Canvas c, Map map, PidValveDto v)
    {
        var pos = map.Pg((double)v.X, (double)v.Y);
        var rot = (double)v.RotationDeg;
        var p = new Placer(pos.X, pos.Y, rot, map.K);

        c.Stroke(0.12, 0.16, 0.2);
        c.Fill(1, 1, 1);
        c.Width(0.9);
        c.Dash(null);
        DrawValveSymbol(c, p, v.ValveType);

        var m = Math.Abs(rot % 180.0);
        var vertical = m > 45.0 && m < 135.0;
        var tag = (v.Tag ?? string.Empty) + (v.IsNormallyOpen ? string.Empty : " (NC)");
        var size = Fs(9, map.K);

        if (vertical) c.Text(tag, pos.X + 16 * map.K, pos.Y, size, 'l', false);
        else c.Text(tag, pos.X, pos.Y - 24 * map.K, size, 'c', false);
    }

    private static void DrawValveSymbol(Canvas c, Placer p, PidValveType type)
    {
        List<(double X, double Y)> L(params (double, double)[] pts) => pts.Select(q => p.Pt(q.Item1, q.Item2)).ToList();
        void Ln((double X, double Y) a, (double X, double Y) b) => c.Line(a.X, a.Y, b.X, b.Y);
        void Bowtie() => c.Poly(L((-10, -7), (10, 7), (10, -7), (-10, 7)), true, true);

        switch (type)
        {
            case PidValveType.Ball:
            case PidValveType.Globe:
                Bowtie();
                var bc = p.Pt(0, 0);
                c.Circle(bc.X, bc.Y, 3.5 * p.K, type == PidValveType.Ball);
                break;

            case PidValveType.Butterfly:
                Bowtie();
                Ln(p.Pt(0, -9), p.Pt(0, 9));
                break;

            case PidValveType.Check:
                Bowtie();
                c.Poly(L((-4, 0), (4, -5), (4, 5)), true, true);
                break;

            case PidValveType.Relief:
                c.Poly(L((-10, 8), (0, -2), (10, 8)), true, true);
                Ln(p.Pt(0, -2), p.Pt(0, -12));
                Ln(p.Pt(-5, -12), p.Pt(5, -12));
                break;

            case PidValveType.Control:
                Bowtie();
                Ln(p.Pt(0, 0), p.Pt(0, -10));
                c.Poly(Arc(p, 0, -10, 7, 6, Math.PI, 2 * Math.PI, 16), true, true);
                break;

            case PidValveType.Solenoid:
                Bowtie();
                Ln(p.Pt(0, 0), p.Pt(0, -9));
                c.Poly(L((-5, -15), (5, -15), (5, -9), (-5, -9)), true, true);
                break;

            default:
                Bowtie();
                Ln(p.Pt(0, -8), p.Pt(0, 0));
                Ln(p.Pt(-4, -8), p.Pt(4, -8));
                break;
        }
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
            var dd = (px - qx) * (px - qx) + (py - qy) * (py - qy);
            if (dd < best) { best = dd; bx = qx; by = qy; }
        }

        return (bx, by);
    }

    private static void DrawInstrument(
        Canvas c, Map map, PidInstrumentDto i,
        Dictionary<Guid, PidNodeDto> nodes, Dictionary<Guid, PidLineDto> lines)
    {
        var x = (double)i.X;
        var y = (double)i.Y;
        var pos = map.Pg(x, y);

        double? ax = null, ay = null;
        if (i.PidEquipmentNodeId.HasValue && nodes.TryGetValue(i.PidEquipmentNodeId.Value, out var nd))
        {
            var (_, h) = PidAutoLayoutService.SymbolSize(nd.SymbolType);
            ax = (double)nd.X;
            ay = (double)nd.Y - (double)h / 2;
        }
        else if (i.PidLineId.HasValue && lines.TryGetValue(i.PidLineId.Value, out var ln) && ln.Points.Count > 1)
        {
            var q = Nearest(ln.Points, x, y);
            ax = q.X;
            ay = q.Y;
        }

        if (ax.HasValue && ay.HasValue)
        {
            var a = map.Pg(ax.Value, ay.Value);
            c.Stroke(0.35, 0.38, 0.42);
            c.Width(0.5);
            c.Dash(new[] { 2.0, 2.0 });
            c.Line(pos.X, pos.Y, a.X, a.Y);
            c.Dash(null);
        }

        DrawBubble(c, pos.X, pos.Y, map.K, i.InstrumentType, StripPrefix(i.Tag));
        c.Text(i.Tag, pos.X, pos.Y - 28 * map.K, Fs(9, map.K), 'c', false);
    }

    private static void DrawBubble(Canvas c, double px, double py, double k, PidInstrumentType type, string number)
    {
        c.Stroke(0.12, 0.16, 0.2);
        c.Fill(1, 1, 1);
        c.Width(0.9);
        c.Dash(null);
        c.Circle(px, py, 15 * k, true);
        c.Line(px - 15 * k, py, px + 15 * k, py);
        c.Text(type.ToString(), px, py + 7 * k, Fs(9, k), 'c', true);
        c.Text(number, px, py - 7 * k, Fs(7.5, k), 'c', false);
    }

    /* ---------- legend and title block ---------- */

    private static void DrawLegend(Canvas c, PidDiagramDto d, double x, double top)
    {
        var y = top;
        c.Text("LEGEND", x, y, 8, 'l', true);
        y -= 16;

        var mediums = d.Lines.Select(l => l.Medium).Distinct().OrderBy(m => (int)m).ToList();
        var px = x;
        foreach (var m in mediums)
        {
            var md = MediumOf(m);
            c.Stroke(md.R, md.G, md.B);
            c.Width(1.6);
            c.Dash(md.Dash);
            c.Line(px, y, px + 30, y);
            c.Dash(null);
            c.Text(MediumLabel(m), px + 34, y, 6.5, 'l', false);
            px += 95;
        }
        if (mediums.Count > 0) y -= 22;

        var valveTypes = d.Valves.Select(v => v.ValveType).Distinct().OrderBy(t => (int)t).ToList();
        px = x + 14;
        foreach (var t in valveTypes)
        {
            c.Stroke(0.12, 0.16, 0.2);
            c.Fill(1, 1, 1);
            c.Width(0.9);
            c.Dash(null);
            DrawValveSymbol(c, new Placer(px, y, 0, 0.9), t);
            c.Text(t.ToString().ToUpperInvariant() + " VALVE", px + 16, y, 6.5, 'l', false);
            px += 95;
        }
        if (valveTypes.Count > 0) y -= 26;

        var instTypes = d.Instruments.Select(i => i.InstrumentType).Distinct().OrderBy(t => (int)t).ToList();
        px = x + 14;
        foreach (var t in instTypes)
        {
            DrawBubble(c, px, y, 0.7, t, "000");
            var name = InstrumentNames.TryGetValue(t, out var nm) ? nm : t.ToString();
            c.Text(name, px + 16, y, 6.5, 'l', false);
            px += 130;
        }
    }

    private static void DrawTitleBlock(Canvas c, PidDiagramDto d, string projectName, DateTime date)
    {
        const double rowH = 14.0;
        const double titleH = 20.0;
        const double labelW = 90.0;

        var rows = new List<(string Label, string Value)>
        {
            ("PROJECT", projectName ?? string.Empty),
            ("DRAWING TITLE", d.Title ?? string.Empty),
            ("DRAWING NO / REV", $"{d.DrawingNo} / R{d.RevisionNo}"),
            ("STATUS", d.Status.ToString().ToUpperInvariant()),
            ("DATE", date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant())
        };

        var x = PageW - Margin - TitleW - 6;
        var bottom = Margin + 6;
        var total = titleH + rows.Count * rowH + rowH;
        var top = bottom + total;

        c.Stroke(0.12, 0.16, 0.2);
        c.Width(1.0);
        c.Dash(null);
        c.Poly(new List<(double, double)> { (x, bottom), (x + TitleW, bottom), (x + TitleW, top), (x, top) }, true, false);

        var cursor = top - titleH;
        c.Text("PIPING AND INSTRUMENTATION DIAGRAM (P&ID)", x + TitleW / 2, top - titleH / 2, 9, 'c', true);
        c.Line(x, cursor, x + TitleW, cursor);

        foreach (var (label, value) in rows)
        {
            var mid = cursor - rowH / 2;
            c.Text(label, x + 4, mid, 6.5, 'l', true);
            c.Text(value, x + labelW + 4, mid, 7.5, 'l', false);
            cursor -= rowH;
            c.Line(x, cursor, x + TitleW, cursor);
        }

        c.Line(x + labelW, top - titleH, x + labelW, cursor);
        c.Text(Disclaimer, x + 4, cursor - rowH / 2, 4.6, 'l', false);
    }

    /* ---------- pdf assembly ---------- */

    private static byte[] Assemble(string content)
    {
        var body = Encoding.ASCII.GetBytes(content);
        using var ms = new MemoryStream();
        var offsets = new List<long>();

        void W(string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            ms.Write(bytes, 0, bytes.Length);
        }

        void Obj(string text)
        {
            offsets.Add(ms.Position);
            W(text);
        }

        W("%PDF-1.4\n");
        Obj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Obj($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(PageW)} {N(PageH)}] " +
            "/Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>\nendobj\n");

        offsets.Add(ms.Position);
        W($"4 0 obj\n<< /Length {body.Length} >>\nstream\n");
        ms.Write(body, 0, body.Length);
        W("\nendstream\nendobj\n");

        Obj("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        Obj("6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        var xref = ms.Position;
        W("xref\n0 7\n0000000000 65535 f \n");
        foreach (var o in offsets)
            W(o.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        W($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return ms.ToArray();
    }
}
