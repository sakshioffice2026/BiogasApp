using System.Globalization;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

namespace BiogasApp.Layout.Output;

public static class PfdDxfWriter
{
    public const string Disclaimer =
        "DRAFT PFD - STANDARD CBG FLOW ORDER, NOT A DESIGN OR COMPLIANCE APPROVAL. VERIFY WITH A QUALIFIED ENGINEER.";

    private const string PipeSolid = "PIPE_SOLID";
    private const string PipeLiquid = "PIPE_LIQUID";
    private const string PipeGas = "PIPE_GAS";

    private const double ColStep = 90.0;
    private const double BoxW = 50.0;
    private const double BoxH = 24.0;
    private const double CircleR = 16.0;
    private const double StackStep = 40.0;
    private const double DigestateRowY = -110.0;
    private const double UtilityRowY = -240.0;

    private static readonly string[][] MainColumns =
    {
        new[] { "FB", "FS" },
        new[] { "MT" },
        new[] { "FT" },
        new[] { "AD" },
        new[] { "SU" },
        new[] { "CF" }
    };

    private static readonly string[] MainPipes = { PipeSolid, PipeLiquid, PipeLiquid, PipeGas, PipeGas };

    private static readonly string[] DigestateOutputs = { "LG", "WY", "DY", "MB" };

    private static readonly HashSet<string> SiteOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "SEC", "ADM", "PK", "WB"
    };

    private sealed class Node
    {
        public string Prefix = string.Empty;
        public string Label = string.Empty;
        public string Name = string.Empty;
        public string Size = string.Empty;
        public List<string> NameLines = new();
        public bool Circle;
        public int Qty;
        public double Radius = CircleR;
        public double Height = BoxH;
        public double Cx;
        public double Cy;

        public double HalfW => Circle ? Radius : BoxW / 2.0;
        public double HalfH => Circle ? Radius : Height / 2.0;
        public double Left => Cx - HalfW;
        public double Right => Cx + HalfW;
        public double Top => Cy + HalfH;
        public double Bottom => Cy - HalfH;
    }

    public static byte[] Write(IReadOnlyList<EquipmentInput> equipment, TitleBlockInfo info)
    {
        var doc = new DxfDocument();
        doc.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
        CreateLayers(doc);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allNodes = new List<Node>();

        Node? Get(string prefix)
        {
            var n = BuildNode(equipment, prefix);
            if (n != null)
            {
                used.Add(prefix);
                allNodes.Add(n);
            }
            return n;
        }

        // Main process row
        var cols = new List<(List<Node> Nodes, string Pipe)>();
        double x = 0;
        for (int i = 0; i < MainColumns.Length; i++)
        {
            var nodes = new List<Node>();
            foreach (var p in MainColumns[i])
            {
                var n = Get(p);
                if (n != null) nodes.Add(n);
            }
            if (nodes.Count == 0) continue;

            for (int j = 0; j < nodes.Count; j++)
            {
                nodes[j].Cx = x;
                nodes[j].Cy = (j - (nodes.Count - 1) / 2.0) * StepOf(nodes);
            }
            x += ColStep;
            cols.Add((nodes, i < MainPipes.Length ? MainPipes[i] : PipeGas));
        }

        for (int k = 0; k < cols.Count - 1; k++)
            Connect(doc, cols[k].Nodes, cols[k + 1].Nodes, cols[k].Pipe);

        var ad = allNodes.FirstOrDefault(n => n.Prefix == "AD");
        var su = allNodes.FirstOrDefault(n => n.Prefix == "SU");

        // Flare branch
        Node? fl = (ad != null && su != null) ? Get("FL") : null;
        if (fl != null && ad != null && su != null)
        {
            fl.Cx = (ad.Right + su.Left) / 2.0;
            fl.Cy = 75;
            AddLine(doc, PipeGas, fl.Cx, 0, fl.Cx, fl.Bottom);
            AddText(doc, "TEXT", "EXCESS BIOGAS", fl.Cx + 4, (fl.Bottom) / 2.0 + 6, 2.0, TextAlignment.MiddleLeft);
        }

        // Digestate row
        var digCols = new List<List<Node>>();
        double dx = (ad?.Cx ?? 0) + ColStep;

        var dt = Get("DT");
        if (dt != null) digCols.Add(new List<Node> { dt });
        var sls = Get("SLS");
        if (sls != null) digCols.Add(new List<Node> { sls });

        var outs = new List<Node>();
        foreach (var p in DigestateOutputs)
        {
            var n = Get(p);
            if (n != null) outs.Add(n);
        }
        if (outs.Count > 0) digCols.Add(outs);

        foreach (var col in digCols)
        {
            for (int j = 0; j < col.Count; j++)
            {
                col[j].Cx = dx;
                col[j].Cy = DigestateRowY + (j - (col.Count - 1) / 2.0) * StepOf(col);
            }
            dx += ColStep;
        }

        for (int k = 0; k < digCols.Count - 1; k++)
            Connect(doc, digCols[k], digCols[k + 1], PipeLiquid);

        if (ad != null && digCols.Count > 0)
        {
            foreach (var n in digCols[0])
            {
                AddLine(doc, PipeLiquid, ad.Cx, ad.Bottom, ad.Cx, n.Cy);
                AddLine(doc, PipeLiquid, ad.Cx, n.Cy, n.Left, n.Cy);
                Arrow(doc, PipeLiquid, n.Left, n.Cy);
            }
        }

        // Utilities and any custom equipment
        var others = new List<string>();
        foreach (var e in equipment)
        {
            string p = PrefixOf(e.Tag);
            if (p.Length == 0 || used.Contains(p) || SiteOnly.Contains(p) || others.Contains(p)) continue;
            others.Add(p);
        }

        int idx = 0;
        foreach (var p in others)
        {
            var n = Get(p);
            if (n == null) continue;
            n.Cx = (idx % 6) * ColStep;
            n.Cy = UtilityRowY - (idx / 6) * 70.0;
            idx++;
        }

        if (allNodes.Count == 0)
            throw new InvalidOperationException("No equipment to draw in the PFD.");

        foreach (var n in allNodes) DrawNode(doc, n);

        double minX = allNodes.Min(n => n.Left);
        double maxX = allNodes.Max(n => n.Right);
        double minY = allNodes.Min(n => n.Bottom);
        double maxY = allNodes.Max(n => n.Top);
        double midX = (minX + maxX) / 2.0;

        AddText(doc, "TEXT", "PROCESS FLOW DIAGRAM - NOT TO SCALE", midX, maxY + 25, 5.0, TextAlignment.MiddleCenter);
        AddText(doc, "TEXT", "MAIN PROCESS", minX, 40, 2.5, TextAlignment.MiddleLeft);
        if (digCols.Count > 0)
            AddText(doc, "TEXT", "DIGESTATE HANDLING", minX, DigestateRowY + 70, 2.5, TextAlignment.MiddleLeft);
        if (idx > 0)
            AddText(doc, "TEXT", "UTILITIES AND SUPPORT (NO PIPING DRAWN)", minX, UtilityRowY + 30, 2.5, TextAlignment.MiddleLeft);

        double legendY = minY - 30;
        DrawLegend(doc, minX, legendY);
        DrawTitleBlock(doc, info, minX, legendY - 15);

        using var ms = new MemoryStream();
        if (!doc.Save(ms)) throw new InvalidOperationException("DXF save failed.");
        return ms.ToArray();
    }

    private static void CreateLayers(DxfDocument doc)
    {
        var defs = new (string Name, AciColor Color, Lineweight Weight)[]
        {
            ("BOX", new AciColor(7), Lineweight.W70),
            (PipeSolid, new AciColor(110, 55, 0), Lineweight.W50),
            (PipeLiquid, new AciColor(0, 0, 150), Lineweight.W50),
            (PipeGas, new AciColor(0, 100, 100), Lineweight.W50),
            ("TEXT", new AciColor(7), Lineweight.Default),
            ("TITLE", new AciColor(7), Lineweight.Default)
        };
        foreach (var d in defs)
            doc.Layers.Add(new Layer(d.Name) { Color = d.Color, Lineweight = d.Weight });
    }

    private static Node? BuildNode(IReadOnlyList<EquipmentInput> equipment, string prefix)
    {
        var matches = equipment
            .Where(e => string.Equals(PrefixOf(e.Tag), prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0) return null;

        var first = matches[0];
        int qty = matches.Sum(e => Math.Max(1, e.Quantity));
        bool circle = first.Shape == EquipmentShape.Circle;
        double dia = first.DiameterM > 0 ? first.DiameterM : first.WidthM;

        var node = new Node
        {
            Prefix = prefix.ToUpperInvariant(),
            Label = qty == 1
                ? first.Tag.ToUpperInvariant()
                : $"{prefix.ToUpperInvariant()}-01 TO {prefix.ToUpperInvariant()}-{qty:00}",
            Name = first.Name,
            Circle = circle,
            Qty = qty,
            Size = circle
                ? $"DIA {Fmt(dia)} m"
                : $"{Fmt(first.WidthM)} x {Fmt(first.LengthM)} m"
        };

        node.NameLines = Wrap(node.Name, circle ? 18 : 28);
        int count = node.NameLines.Count + 2;
        node.Radius = Math.Max(CircleR, count * 3.2 + 4.0);
        node.Height = Math.Max(BoxH, count * 4.2 + 6.0);
        return node;
    }

    private static string PrefixOf(string? tag)
        => new string((tag ?? string.Empty).TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static List<string> Wrap(string? s, int max)
    {
        var lines = new List<string>();
        var cur = string.Empty;

        foreach (var raw in (s ?? string.Empty).ToUpperInvariant()
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw;
            while (word.Length > max)
            {
                if (cur.Length > 0)
                {
                    lines.Add(cur);
                    cur = string.Empty;
                }
                lines.Add(word.Substring(0, max));
                word = word.Substring(max);
            }

            if (cur.Length == 0) cur = word;
            else if (cur.Length + 1 + word.Length <= max) cur += " " + word;
            else
            {
                lines.Add(cur);
                cur = word;
            }
        }

        if (cur.Length > 0) lines.Add(cur);
        if (lines.Count == 0) lines.Add(string.Empty);
        return lines;
    }

    private static double StepOf(List<Node> nodes)
        => Math.Max(StackStep, nodes.Max(n => n.Circle ? n.Radius * 2.0 : n.Height) + 8.0);

    private static void DrawNode(DxfDocument doc, Node n)
    {
        string sizeLine = $"{n.Size}  QTY {n.Qty}";

        var lines = new List<(string Text, double H)> { (n.Label, n.Circle ? 2.2 : 2.6) };
        foreach (var l in n.NameLines) lines.Add((l, n.Circle ? 1.8 : 2.0));
        lines.Add((sizeLine, 1.8));

        if (n.Circle)
        {
            Add(doc, "BOX", new Circle(new Vector2(n.Cx, n.Cy), n.Radius));
        }
        else
        {
            Add(doc, "BOX", new Polyline2D(new List<Vector2>
            {
                new(n.Left, n.Bottom), new(n.Right, n.Bottom), new(n.Right, n.Top), new(n.Left, n.Top)
            }, true));
        }

        double step = n.Circle ? 3.6 : 4.2;
        double y = n.Cy + (lines.Count - 1) * step / 2.0;
        foreach (var (text, h) in lines)
        {
            AddText(doc, "TEXT", text, n.Cx, y, h, TextAlignment.MiddleCenter);
            y -= step;
        }
    }

    private static void Connect(DxfDocument doc, List<Node> from, List<Node> to, string pipe)
    {
        double xa = from.Max(n => n.Right);
        double xb = to.Min(n => n.Left);
        double xm = (xa + xb) / 2.0;

        foreach (var a in from)
            AddLine(doc, pipe, a.Right, a.Cy, xm, a.Cy);

        foreach (var b in to)
        {
            AddLine(doc, pipe, xm, b.Cy, b.Left, b.Cy);
            Arrow(doc, pipe, b.Left, b.Cy);
        }

        double lo = Math.Min(from.Min(n => n.Cy), to.Min(n => n.Cy));
        double hi = Math.Max(from.Max(n => n.Cy), to.Max(n => n.Cy));
        if (hi - lo > 1e-6)
            AddLine(doc, pipe, xm, lo, xm, hi);
    }

    private static void Arrow(DxfDocument doc, string layer, double x, double y)
    {
        AddLine(doc, layer, x, y, x - 3, y + 1.2);
        AddLine(doc, layer, x, y, x - 3, y - 1.2);
    }

    private static void DrawLegend(DxfDocument doc, double x, double y)
    {
        var items = new (string Layer, string Text)[]
        {
            (PipeSolid, "SOLIDS (FEEDSTOCK, DIGESTATE CAKE)"),
            (PipeLiquid, "LIQUID (SLURRY, DIGESTATE)"),
            (PipeGas, "BIOGAS / BIOMETHANE")
        };

        double cursor = x;
        foreach (var (layer, text) in items)
        {
            AddLine(doc, layer, cursor, y, cursor + 20, y);
            AddText(doc, "TEXT", text, cursor + 24, y, 2.0, TextAlignment.MiddleLeft);
            cursor += 130;
        }
    }

    private static void DrawTitleBlock(DxfDocument doc, TitleBlockInfo info, double x, double top)
    {
        const double width = 170.0;
        const double labelWidth = 40.0;
        const double rowH = 7.0;
        const double titleH = 9.0;

        var rows = new List<(string Label, string Value)>
        {
            ("PROJECT", info.ProjectName),
            ("LOCATION", info.Location),
            ("CAPACITY", info.CapacityText),
            ("DRAWING NO / REV", $"{info.DrawingNo} / {info.Revision}"),
            ("DATE", info.Date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant())
        };

        double total = titleH + rows.Count * rowH + rowH;
        double bottom = top - total;

        Add(doc, "TITLE", new Polyline2D(new List<Vector2>
        {
            new(x, bottom), new(x + width, bottom), new(x + width, top), new(x, top)
        }, true));

        double cursor = top - titleH;
        AddText(doc, "TITLE", "PROCESS FLOW DIAGRAM (PFD)", x + width / 2, top - titleH / 2, 3.5, TextAlignment.MiddleCenter);
        AddLine(doc, "TITLE", x, cursor, x + width, cursor);

        foreach (var (label, value) in rows)
        {
            double mid = cursor - rowH / 2;
            AddText(doc, "TITLE", label, x + 2, mid, 2.2, TextAlignment.MiddleLeft);
            AddText(doc, "TITLE", value ?? string.Empty, x + labelWidth + 2, mid, 2.2, TextAlignment.MiddleLeft);
            cursor -= rowH;
            AddLine(doc, "TITLE", x, cursor, x + width, cursor);
        }

        AddLine(doc, "TITLE", x + labelWidth, top - titleH, x + labelWidth, cursor);
        AddText(doc, "TITLE", Disclaimer, x + 2, cursor - rowH / 2, 1.8, TextAlignment.MiddleLeft);
    }

    private static void Add(DxfDocument doc, string layer, EntityObject entity)
    {
        entity.Layer = doc.Layers[layer];
        doc.Entities.Add(entity);
    }

    private static void AddLine(DxfDocument doc, string layer, double x1, double y1, double x2, double y2)
        => Add(doc, layer, new Line(new Vector2(x1, y1), new Vector2(x2, y2)));

    private static void AddText(DxfDocument doc, string layer, string value, double x, double y,
        double height, TextAlignment alignment)
    {
        var t = new Text(value, new Vector2(x, y), height) { Alignment = alignment };
        Add(doc, layer, t);
    }
}