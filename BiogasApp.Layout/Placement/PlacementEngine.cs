using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Rules;
using BiogasApp.Layout.Sizing;

namespace BiogasApp.Layout.Placement;

public sealed class PlacementEngine
{
    private const double MinGapM = 3.0;
    private const double RingStepM = 4.0;
    private const int AnglesPerRing = 16;
    private const double RoadAccessM = 12.0;
    private const double RoadClearM = 0.5;

    private readonly IClearanceRuleProvider _rules;
    private readonly ClearanceChecker _checker;

    private sealed record Geo(PlacedItem Item, List<Point2D> Fp, Point2D Min, Point2D Max);

    public PlacementEngine(IClearanceRuleProvider rules)
    {
        _rules = rules;
        _checker = new ClearanceChecker(rules);
    }

    public LayoutResult Generate(LayoutInput input)
    {
        var zones = ZoneAssigner.Assign(input);
        var network = RoadPlanner.Plan(input, zones, _rules);
        var all = FootprintBuilder.Expand(input.Equipment);
        var geos = new List<Geo>();

        foreach (var item in all)
        {
            var lk = input.LockedItems.FirstOrDefault(
                l => l.EquipmentId == item.EquipmentId && l.InstanceNo == item.InstanceNo);
            if (lk == null) continue;
            item.X = lk.X;
            item.Y = lk.Y;
            item.RotationDeg = lk.RotationDeg;
            item.IsLocked = true;
            geos.Add(MakeGeo(item));
        }

        var pending = all.Where(i => !i.IsLocked)
    .OrderBy(i => IsWeighbridge(i) ? 0 : 1)
    .ThenBy(i => ZoneOrder(i.Zone))
    .ThenByDescending(i => i.WidthM * i.LengthM)
    .ToList();

        foreach (var item in pending)
        {
            if (IsWeighbridge(item)) PlaceWeighbridge(item, network);
            else PlaceOne(input, zones, network, geos, item);
            geos.Add(MakeGeo(item));
        }

        return Build(input, all, network);
    }

    public LayoutResult Evaluate(LayoutInput input, List<PlacedItem> items)
    {
        var zones = ZoneAssigner.Assign(input);
        var network = RoadPlanner.Plan(input, zones, _rules);
        return Build(input, items, network);
    }

    private LayoutResult Build(LayoutInput input, List<PlacedItem> items, RoadNetwork network)
    {
        var polylines = network.Roads.Select(r => r.Points).ToList();
        return new LayoutResult
        {
            Items = items,
            Roads = polylines,
            Warnings = _checker.CheckAll(input, items, polylines),
            GatePoint = network.Gate,
            RuleSetId = _rules.RuleSetId,
            RuleSetVersion = _rules.RuleSetVersion,
            IsDraft = true
        };
    }

    private void PlaceOne(LayoutInput input, ZonePlan zones, RoadNetwork net, List<Geo> geos, PlacedItem item)
    {
        var anchor = zones.Anchors[item.Zone];
        var rots = Rotations(item, net);

        if (Search(input, net, geos, item, anchor, rots, false)) return;
        if (Search(input, net, geos, item, anchor, rots, true)) return;
        if (SearchGrid(input, net, geos, item, anchor, rots)) return;
        item.X = anchor.X;
        item.Y = anchor.Y;
        item.RotationDeg = rots[0];
    }

    private bool Search(LayoutInput input, RoadNetwork net, List<Geo> geos, PlacedItem item,
        Point2D anchor, double[] rots, bool relaxRoads)
    {
        var (bbMin, bbMax) = PolygonOps.BoundingBox(input.PlotCorners);
        double maxR = bbMin.DistanceTo(bbMax);

        double bestCost = double.MaxValue;
        double bx = 0, by = 0, br = 0;
        bool found = false;

        for (double r = 0; r <= maxR; r += RingStepM)
        {
            int count = r == 0 ? 1 : AnglesPerRing;
            for (int k = 0; k < count; k++)
            {
                double ang = 2 * Math.PI * k / count;
                double x = anchor.X + r * Math.Cos(ang);
                double y = anchor.Y + r * Math.Sin(ang);

                foreach (var rot in rots)
                {
                    item.X = x;
                    item.Y = y;
                    item.RotationDeg = rot;

                    double cost = CandidateCost(input, net, geos, item, relaxRoads);
                    if (double.IsNaN(cost)) continue;
                    cost += r;

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bx = x;
                        by = y;
                        br = rot;
                        found = true;
                    }
                }
            }
            if (found && bestCost <= r) break;
        }

        if (found)
        {
            item.X = bx;
            item.Y = by;
            item.RotationDeg = br;
        }
        return found;
    }
    private bool SearchGrid(LayoutInput input, RoadNetwork net, List<Geo> geos, PlacedItem item,
    Point2D anchor, double[] rots)
    {
        const double step = 3.0;
        var (bbMin, bbMax) = PolygonOps.BoundingBox(input.PlotCorners);

        double bestCost = double.MaxValue;
        double bx = 0, by = 0, br = 0;
        bool found = false;

        for (double x = bbMin.X; x <= bbMax.X; x += step)
        {
            for (double y = bbMin.Y; y <= bbMax.Y; y += step)
            {
                foreach (var rot in rots)
                {
                    item.X = x;
                    item.Y = y;
                    item.RotationDeg = rot;

                    double cost = CandidateCost(input, net, geos, item, true);
                    if (double.IsNaN(cost)) continue;
                    cost += new Point2D(x, y).DistanceTo(anchor);

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bx = x;
                        by = y;
                        br = rot;
                        found = true;
                    }
                }
            }
        }

        if (found)
        {
            item.X = bx;
            item.Y = by;
            item.RotationDeg = br;
        }
        return found;
    }
    private double CandidateCost(LayoutInput input, RoadNetwork net, List<Geo> geos, PlacedItem item, bool relaxRoads)
    {
        var fp = item.GetFootprint();
        if (!PolygonOps.ContainsPolygon(input.PlotCorners, fp)) return double.NaN;

        foreach (var z in input.NoBuildZones)
        {
            if (z.Polygon.Count >= 3 && PolygonOps.Overlaps(fp, z.Polygon)) return double.NaN;
        }

        var (min, max) = PolygonOps.BoundingBox(fp);
        double penalty = 0;

        foreach (var g in geos)
        {
            var rule = RuleBothWays(item.HazardClass, g.Item.HazardClass);
            double required = Math.Max(MinGapM, rule?.MinDistanceM ?? 0);
            if (AabbGap(min, max, g.Min, g.Max) >= required) continue;

            double d = PolygonOps.DistanceBetween(fp, g.Fp);
            if (d < MinGapM) return double.NaN;
            if (rule != null && d < rule.MinDistanceM)
                penalty += 100 + (rule.MinDistanceM - d) * 5;
        }

        var boundary = RuleBothWays(item.HazardClass, HazardClass.Boundary);
        if (boundary != null)
        {
            double d = fp.Min(p => PolygonOps.DistanceToEdges(input.PlotCorners, p));
            if (d < boundary.MinDistanceM)
                penalty += 100 + (boundary.MinDistanceM - d) * 5;
        }

        var receptorRule = RuleBothWays(item.HazardClass, HazardClass.Receptor);
        if (receptorRule != null)
        {
            foreach (var r in input.Receptors)
            {
                double d = PolygonOps.Contains(fp, r.Position) ? 0 : PolygonOps.DistanceToEdges(fp, r.Position);
                if (d < receptorRule.MinDistanceM)
                    penalty += 100 + (receptorRule.MinDistanceM - d) * 5;
            }
        }

        var roadRule = RuleBothWays(item.HazardClass, HazardClass.Road);
        double nearestRoadGap = double.MaxValue;

        foreach (var road in net.Roads)
        {
            double half = road.WidthM / 2.0;
            double dmin = double.MaxValue;
            for (int i = 0; i < road.Points.Count - 1; i++)
            {
                for (int j = 0; j < fp.Count; j++)
                {
                    double x = PolygonOps.DistanceBetweenSegments(
                        road.Points[i], road.Points[i + 1], fp[j], fp[(j + 1) % fp.Count]);
                    if (x < dmin) dmin = x;
                }
            }

            double gap = dmin - half;
            if (gap < RoadClearM)
            {
                if (!relaxRoads) return double.NaN;
                penalty += 200;
            }
            else if (roadRule != null && gap < roadRule.MinDistanceM)
            {
                penalty += 100 + (roadRule.MinDistanceM - gap) * 5;
            }

            if (gap < nearestRoadGap) nearestRoadGap = gap;
        }

        if (item.NeedsRoadAccess && nearestRoadGap > RoadAccessM)
            penalty += (nearestRoadGap - RoadAccessM) * 3;

        return penalty;
    }

    private void PlaceWeighbridge(PlacedItem item, RoadNetwork net)
    {
        item.X = net.Weighbridge.X;
        item.Y = net.Weighbridge.Y;
        item.RotationDeg = Math.Atan2(net.Inward.Y, net.Inward.X) * 180.0 / Math.PI - 90.0;
    }

    private static bool IsWeighbridge(PlacedItem item)
        => item.Tag.StartsWith("WB", StringComparison.OrdinalIgnoreCase)
           || item.Name.Contains("weigh", StringComparison.OrdinalIgnoreCase);

    private static double[] Rotations(PlacedItem item, RoadNetwork net)
    {
        if (item.Shape == EquipmentShape.Circle) return new[] { 0.0 };
        double baseRot = Math.Atan2(net.Tangent.Y, net.Tangent.X) * 180.0 / Math.PI;
        return new[] { baseRot, baseRot + 90.0 };
    }

    private static int ZoneOrder(EquipmentZone zone) => zone switch
    {
        EquipmentZone.Dirty => 0,
        EquipmentZone.Gas => 1,
        EquipmentZone.Process => 2,
        _ => 3
    };

    private ClearanceRuleValue? RuleBothWays(HazardClass a, HazardClass b)
        => _rules.GetRule(a, b) ?? _rules.GetRule(b, a);

    private static Geo MakeGeo(PlacedItem item)
    {
        var fp = item.GetFootprint();
        var (min, max) = PolygonOps.BoundingBox(fp);
        return new Geo(item, fp, min, max);
    }

    private static double AabbGap(Point2D aMin, Point2D aMax, Point2D bMin, Point2D bMax)
    {
        double dx = Math.Max(0, Math.Max(aMin.X - bMax.X, bMin.X - aMax.X));
        double dy = Math.Max(0, Math.Max(aMin.Y - bMax.Y, bMin.Y - aMax.Y));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}