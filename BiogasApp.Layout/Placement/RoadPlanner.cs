using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Rules;

namespace BiogasApp.Layout.Placement;

public enum RoadKind
{
    Heavy = 1,
    Main = 2,
    Loop = 3,
    FireAccess = 4
}

public sealed record RoadSegment(RoadKind Kind, double WidthM, List<Point2D> Points);

public sealed record RoadNetwork(
    List<RoadSegment> Roads,
    Point2D Gate,
    Point2D Weighbridge,
    Point2D Inward,
    Point2D Tangent);

public static class RoadPlanner
{
    private const double WeighbridgeDistM = 15.0;
    private const double LoopMarginM = 20.0;
    private const double FireHalfM = 35.0;

    public static RoadNetwork Plan(LayoutInput input, ZonePlan zones, IClearanceRuleProvider? rules = null)
    {
        double mainWidth = Setting(rules, "road_main_width_m", 6.0);
        double heavyWidth = Setting(rules, "road_heavy_width_m", 9.0);

        var plot = input.PlotCorners;
        var gate = zones.Gate;
        var centroid = zones.Centroid;

        int i = ((input.GateEdgeIndex % plot.Count) + plot.Count) % plot.Count;
        var edge = plot[(i + 1) % plot.Count] - plot[i];
        var t = Unit(edge);
        var n = new Point2D(-t.Y, t.X);
        if (!PolygonOps.Contains(plot, gate + n * 1.0)) n = n * -1;

        Point2D ToWorld(double s, double u) => gate + n * s + t * u;
        (double S, double U) ToLocal(Point2D p) => ((p - gate).Dot(n), (p - gate).Dot(t));

        Point2D Clamp(Point2D p)
        {
            for (int k = 0; k < 60 && !PolygonOps.Contains(plot, p); k++)
                p = p + (centroid - p) * 0.1;
            return p;
        }

        List<Point2D> Route(Point2D a, Point2D b)
        {
            var (_, ua) = ToLocal(a);
            var (sb, _) = ToLocal(b);
            return new List<Point2D> { a, Clamp(ToWorld(sb, ua)), b };
        }

        List<Point2D> Ring(double sMin, double sMax, double uMin, double uMax)
        {
            var ring = new List<Point2D>
            {
                Clamp(ToWorld(sMin, uMin)),
                Clamp(ToWorld(sMin, uMax)),
                Clamp(ToWorld(sMax, uMax)),
                Clamp(ToWorld(sMax, uMin))
            };
            ring.Add(ring[0]);
            return ring;
        }

        var weigh = Clamp(gate + n * WeighbridgeDistM);
        var clean = zones.Anchors[EquipmentZone.Clean];
        var dirty = zones.Anchors[EquipmentZone.Dirty];
        var process = zones.Anchors[EquipmentZone.Process];
        var gas = zones.Anchors[EquipmentZone.Gas];

        var roads = new List<RoadSegment>();

        var heavyPts = new List<Point2D> { gate };
        heavyPts.AddRange(Route(weigh, dirty));
        roads.Add(new RoadSegment(RoadKind.Heavy, heavyWidth, heavyPts));

        roads.Add(new RoadSegment(RoadKind.Main, mainWidth, Route(gate, clean)));

        var mainPts = Route(weigh, process);
        mainPts.AddRange(Route(process, gas).Skip(1));
        roads.Add(new RoadSegment(RoadKind.Main, mainWidth, mainPts));

        var (sp, up) = ToLocal(process);
        var (sg, ug) = ToLocal(gas);
        roads.Add(new RoadSegment(RoadKind.Loop, mainWidth, Ring(
            Math.Min(sp, sg) - LoopMarginM, Math.Max(sp, sg) + LoopMarginM,
            Math.Min(up, ug) - LoopMarginM, Math.Max(up, ug) + LoopMarginM)));

        roads.Add(new RoadSegment(RoadKind.FireAccess, mainWidth, Ring(
            sg - FireHalfM, sg + FireHalfM, ug - FireHalfM, ug + FireHalfM)));

        return new RoadNetwork(roads, gate, weigh, n, t);
    }

    private static double Setting(IClearanceRuleProvider? rules, string key, double fallback)
        => rules?.GetSetting(key)?.Value ?? fallback;

    private static Point2D Unit(Point2D v)
    {
        double len = v.Length;
        return len < 1e-9 ? new Point2D(1, 0) : v * (1.0 / len);
    }
}