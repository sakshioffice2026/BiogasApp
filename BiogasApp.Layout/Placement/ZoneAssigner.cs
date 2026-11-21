using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;

namespace BiogasApp.Layout.Placement;

public sealed record ZonePlan(
    Point2D Gate,
    Point2D Centroid,
    Dictionary<EquipmentZone, Point2D> Directions,
    Dictionary<EquipmentZone, Point2D> Anchors);

public static class ZoneAssigner
{
    private const double Deg2Rad = Math.PI / 180.0;
    private const double AnchorFraction = 0.55;

    public static Point2D GatePoint(LayoutInput input)
    {
        var c = input.PlotCorners;
        int i = ((input.GateEdgeIndex % c.Count) + c.Count) % c.Count;
        var a = c[i];
        var b = c[(i + 1) % c.Count];
        double len = a.DistanceTo(b);
        if (len < 1e-9) return a;
        double t = Math.Clamp(input.GateOffsetM / len, 0, 1);
        return a + (b - a) * t;
    }

    public static Point2D BearingToVector(double bearingDeg, double northAngleDeg)
    {
        double a = (bearingDeg + northAngleDeg) * Deg2Rad;
        return new Point2D(Math.Sin(a), Math.Cos(a));
    }

    public static ZonePlan Assign(LayoutInput input)
    {
        var plot = input.PlotCorners;
        var centroid = PolygonOps.Centroid(plot);
        var gate = GatePoint(input);

        var clean = Unit(gate - centroid, new Point2D(0, -1));

        var dirty = clean * -1;
        if (input.WindFromDeg.HasValue)
        {
            var downwind = BearingToVector(input.WindFromDeg.Value + 180.0, input.NorthAngleDeg);
            dirty = Unit(dirty + downwind, dirty);
        }

        Point2D gas;
        if (input.GasOfftakeSideDeg.HasValue)
        {
            gas = BearingToVector(input.GasOfftakeSideDeg.Value, input.NorthAngleDeg);
        }
        else
        {
            var left = new Point2D(-clean.Y, clean.X);
            var right = left * -1;
            gas = ReceptorScore(input, centroid, left) <= ReceptorScore(input, centroid, right) ? left : right;
        }

        var directions = new Dictionary<EquipmentZone, Point2D>
        {
            [EquipmentZone.Clean] = clean,
            [EquipmentZone.Dirty] = dirty,
            [EquipmentZone.Gas] = gas,
            [EquipmentZone.Process] = new Point2D(0, 0)
        };

        var anchors = new Dictionary<EquipmentZone, Point2D>();
        foreach (var kv in directions)
        {
            double reach = kv.Key == EquipmentZone.Process ? 0 : RayDistance(plot, centroid, kv.Value) * AnchorFraction;
            anchors[kv.Key] = centroid + kv.Value * reach;
        }

        return new ZonePlan(gate, centroid, directions, anchors);
    }

    private static double ReceptorScore(LayoutInput input, Point2D centroid, Point2D dir)
    {
        var anchor = centroid + dir * RayDistance(input.PlotCorners, centroid, dir) * AnchorFraction;
        double score = 0;
        foreach (var r in input.Receptors)
            score += 1.0 / (1.0 + anchor.DistanceTo(r.Position));
        return score;
    }

    private static Point2D Unit(Point2D v, Point2D fallback)
    {
        double len = v.Length;
        return len < 1e-9 ? fallback : v * (1.0 / len);
    }

    private static double RayDistance(IReadOnlyList<Point2D> poly, Point2D o, Point2D d)
    {
        double best = double.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var e = poly[(i + 1) % poly.Count] - a;
            double denom = d.Cross(e);
            if (Math.Abs(denom) < 1e-12) continue;
            double t = (a - o).Cross(e) / denom;
            double u = (a - o).Cross(d) / denom;
            if (t > 1e-9 && u >= 0 && u <= 1 && t < best) best = t;
        }
        return best == double.MaxValue ? 0 : best;
    }
}