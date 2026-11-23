using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Placement;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

namespace BiogasApp.Layout.Output;

public static class SiteDxfWriter
{
    public static byte[] Write(
        LayoutInput input,
        LayoutResult result,
        IReadOnlyList<RoadSegment> roads,
        TitleBlockInfo info)
    {
        var doc = new DxfDocument();
        doc.DrawingVariables.InsUnits = DrawingUnits.Meters;
        var layers = CreateLayers(doc);

        DrawPlot(doc, layers, input);
        DrawNoBuild(doc, layers, input);
        DrawRoads(doc, layers, roads);
        DrawItems(doc, layers, result);
        DrawDimensions(doc, layers, input, result);
        DrawReceptors(doc, layers, input);
        DrawGate(doc, layers, input, result);
        DrawWarnings(doc, layers, result);

        var (bbMin, bbMax) = PolygonOps.BoundingBox(input.PlotCorners);
        DrawNorthArrow(doc, layers, input, bbMax);

        TitleBlockWriter.Write(doc, info, bbMin.X, bbMin.Y - 15);
        EquipmentScheduleWriter.Write(doc, result.Items, bbMax.X + 30, bbMax.Y);

        using var ms = new MemoryStream();
        if (!doc.Save(ms)) throw new InvalidOperationException("DXF save failed.");
        return ms.ToArray();
    }

    private static Dictionary<string, Layer> CreateLayers(DxfDocument doc)
    {
        var defs = new (string Name, short Color)[]
        {
            ("PLOT", 1), ("NOBUILD", 6), ("ROAD", 8), ("ROAD_CL", 2),
            ("DIRTY", 30), ("PROCESS", 3), ("GAS", 4), ("CLEAN", 5),
            ("TEXT", 7), ("RECEPTORS", 40), ("GATE", 2), ("WARNINGS", 1), ("TITLE", 7), ("DIM", 3)
        };

        var map = new Dictionary<string, Layer>();
        foreach (var d in defs)
        {
            var layer = new Layer(d.Name) { Color = new AciColor(d.Color) };
            if (d.Name == "ROAD_CL") layer.Linetype = Linetype.DashDot;
            doc.Layers.Add(layer);
            map[d.Name] = layer;
        }
        return map;
    }

    private static void Add(DxfDocument doc, Dictionary<string, Layer> layers, EntityObject entity, string layer)
    {
        entity.Layer = layers[layer];
        doc.Entities.Add(entity);
    }

    private static Vector2 V(Point2D p) => new(p.X, p.Y);

    private static void AddText(DxfDocument doc, Dictionary<string, Layer> layers, string value,
        Point2D pos, double height, string layer, double rotationDeg = 0)
    {
        var t = new Text(value, V(pos), height)
        {
            Alignment = TextAlignment.MiddleCenter,
            Rotation = rotationDeg
        };
        Add(doc, layers, t, layer);
    }

    private static void DrawPlot(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input)
    {
        var pl = new Polyline2D(input.PlotCorners.Select(V).ToList(), true);
        Add(doc, layers, pl, "PLOT");

        var c = PolygonOps.Centroid(input.PlotCorners);
        AddText(doc, layers, $"PLOT AREA {PolygonOps.Area(input.PlotCorners):F0} SQM", c, 3.0, "TEXT");
    }

    private static void DrawNoBuild(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input)
    {
        foreach (var z in input.NoBuildZones)
        {
            if (z.Polygon.Count < 3) continue;
            Add(doc, layers, new Polyline2D(z.Polygon.Select(V).ToList(), true), "NOBUILD");
            AddText(doc, layers, $"NO-BUILD: {z.Reason}", PolygonOps.Centroid(z.Polygon), 1.5, "NOBUILD");
        }
    }

    private static void DrawRoads(DxfDocument doc, Dictionary<string, Layer> layers, IReadOnlyList<RoadSegment> roads)
    {
        foreach (var road in roads)
        {
            double half = road.WidthM / 2.0;
            for (int i = 0; i < road.Points.Count - 1; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                var d = b - a;
                double len = d.Length;
                if (len < 1e-6) continue;

                var u = d * (1.0 / len);
                var n = new Point2D(-u.Y, u.X);
                var a2 = a - u * half;
                var b2 = b + u * half;

                var rect = new List<Vector2>
                {
                    V(a2 + n * half), V(b2 + n * half), V(b2 - n * half), V(a2 - n * half)
                };
                Add(doc, layers, new Polyline2D(rect, true), "ROAD");
                Add(doc, layers, new Line(V(a), V(b)), "ROAD_CL");
            }

            if (road.Points.Count >= 2)
            {
                var mid = (road.Points[0] + road.Points[1]) * 0.5;
                AddText(doc, layers, $"{road.WidthM:F0} M {road.Kind.ToString().ToUpperInvariant()}", mid, 1.2, "TEXT");
            }
        }
    }

    private static void DrawItems(DxfDocument doc, Dictionary<string, Layer> layers, LayoutResult result)
    {
        foreach (var item in result.Items)
        {
            string layer = item.Zone switch
            {
                EquipmentZone.Dirty => "DIRTY",
                EquipmentZone.Process => "PROCESS",
                EquipmentZone.Gas => "GAS",
                _ => "CLEAN"
            };

            if (item.Shape == EquipmentShape.Circle)
                Add(doc, layers, new Circle(new Vector2(item.X, item.Y), item.WidthM / 2.0), layer);
            else
                Add(doc, layers, new Polyline2D(item.GetFootprint().Select(V).ToList(), true), layer);

            double h = Math.Clamp(Math.Min(item.WidthM, item.LengthM) / 5.0, 0.8, 3.0);
            string tag = item.InstanceNo > 1 ? $"{item.Tag}-{item.InstanceNo}" : item.Tag;
            AddText(doc, layers, tag, new Point2D(item.X, item.Y + h * 0.7), h, "TEXT");
            AddText(doc, layers, item.Name.ToUpperInvariant(), new Point2D(item.X, item.Y - h * 0.9), h * 0.55, "TEXT");
        }
    }

    private static void DrawReceptors(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input)
    {
        foreach (var r in input.Receptors)
        {
            Add(doc, layers, new Circle(V(r.Position), 3.0), "RECEPTORS");
            string label = string.IsNullOrWhiteSpace(r.Name) ? r.Type.ToString() : $"{r.Type}: {r.Name}";
            AddText(doc, layers, label.ToUpperInvariant(), r.Position + new Point2D(0, 6), 2.0, "RECEPTORS");
        }
    }

    private static void DrawGate(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input, LayoutResult result)
    {
        var g = result.GatePoint;
        Add(doc, layers, new Circle(V(g), 2.0), "GATE");
        AddText(doc, layers, "MAIN GATE", g + new Point2D(0, -5), 2.0, "GATE");
    }

    private static void DrawWarnings(DxfDocument doc, Dictionary<string, Layer> layers, LayoutResult result)
    {
        var flagged = new HashSet<(Guid, int)>();
        foreach (var w in result.Warnings)
        {
            if (w.EquipmentId == null || w.InstanceNo == null) continue;
            if (!flagged.Add((w.EquipmentId.Value, w.InstanceNo.Value))) continue;

            var item = result.Items.FirstOrDefault(i => i.EquipmentId == w.EquipmentId && i.InstanceNo == w.InstanceNo);
            if (item == null) continue;

            double radius = Math.Max(item.WidthM, item.LengthM) / 2.0 + 1.5;
            Add(doc, layers, new Circle(new Vector2(item.X, item.Y), radius), "WARNINGS");
        }
    }
    private static void DrawDimensions(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input, LayoutResult result)
    {
        var plotStyle = new DimensionStyle("BIOGAS_PLOT")
        {
            TextHeight = 1.5,
            ArrowSize = 1.2,
            ExtLineOffset = 0.5,
            ExtLineExtend = 0.8,
            TextOffset = 0.6,
            LengthPrecision = 1
        };

        var itemStyle = new DimensionStyle("BIOGAS_ITEM")
        {
            TextHeight = 1.0,
            ArrowSize = 0.8,
            ExtLineOffset = 0.3,
            ExtLineExtend = 0.5,
            TextOffset = 0.4,
            LengthPrecision = 1
        };

        var pts = input.PlotCorners;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            double len = a.DistanceTo(b);
            if (len < 1.0) continue;

            var d = (b - a) * (1.0 / len);
            var left = new Point2D(-d.Y, d.X);
            var mid = (a + b) * 0.5;
            double off = PolygonOps.Contains(pts, mid + left * 0.5) ? -8.0 : 8.0;
            Add(doc, layers, new AlignedDimension(V(a), V(b), off, plotStyle), "DIM");
        }

        foreach (var item in result.Items)
        {
            if (Math.Min(item.WidthM, item.LengthM) < 2.0) continue;

            if (item.Shape == EquipmentShape.Circle)
            {
                double r = item.WidthM / 2.0;
                var refPt = new Vector2(item.X + r * Math.Cos(Math.PI / 4), item.Y + r * Math.Sin(Math.PI / 4));
                Add(doc, layers, new DiametricDimension(new Vector2(item.X, item.Y), refPt, itemStyle), "DIM");
                continue;
            }

            var fp = item.GetFootprint();
            if (fp.Count < 4) continue;

            for (int e = 0; e < 2; e++)
            {
                var a = fp[e];
                var b = fp[e + 1];
                double len = a.DistanceTo(b);
                if (len < 1.0) continue;

                var d = (b - a) * (1.0 / len);
                var left = new Point2D(-d.Y, d.X);
                var mid = (a + b) * 0.5;
                double off = PolygonOps.Contains(fp, mid + left * 0.3) ? -1.5 : 1.5;
                Add(doc, layers, new AlignedDimension(V(a), V(b), off, itemStyle), "DIM");
            }
        }
    }
    private static void DrawNorthArrow(DxfDocument doc, Dictionary<string, Layer> layers, LayoutInput input, Point2D bbMax)
    {
        var origin = new Point2D(bbMax.X + 10, bbMax.Y - 10);
        var dir = ZoneAssigner.BearingToVector(0, input.NorthAngleDeg);
        var tip = origin + dir * 15;
        Add(doc, layers, new Line(V(origin), V(tip)), "TEXT");
        AddText(doc, layers, "N", tip + dir * 3, 3.0, "TEXT");
    }
}