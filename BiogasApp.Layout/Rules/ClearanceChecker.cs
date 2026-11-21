using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;

namespace BiogasApp.Layout.Rules;

public sealed class ClearanceChecker
{
    private readonly IClearanceRuleProvider _rules;

    public ClearanceChecker(IClearanceRuleProvider rules)
    {
        _rules = rules;
    }

    public List<LayoutWarning> CheckAll(
        LayoutInput input,
        IReadOnlyList<PlacedItem> items,
        IReadOnlyList<List<Point2D>> roads)
    {
        var warnings = new List<LayoutWarning>();
        var footprints = items.Select(i => i.GetFootprint()).ToList();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var fp = footprints[i];

            if (!PolygonOps.ContainsPolygon(input.PlotCorners, fp))
            {
                warnings.Add(Plain("OUTSIDE_PLOT",
                    $"{Label(item)} is outside the plot boundary.", item));
            }

            foreach (var zone in input.NoBuildZones)
            {
                if (zone.Polygon.Count >= 3 && PolygonOps.Overlaps(fp, zone.Polygon))
                {
                    warnings.Add(Plain("NO_BUILD_ZONE",
                        $"{Label(item)} is inside a no-build zone ({zone.Reason}).", item));
                }
            }

            CheckBoundary(input, item, fp, warnings);
            CheckReceptors(input, item, fp, warnings);
            CheckRoads(roads, item, fp, warnings);
        }

        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                CheckPair(items[i], footprints[i], items[j], footprints[j], warnings);
            }
        }

        return warnings;
    }

    private void CheckPair(PlacedItem a, List<Point2D> fa, PlacedItem b, List<Point2D> fb, List<LayoutWarning> warnings)
    {
        double d = PolygonOps.DistanceBetween(fa, fb);

        if (d <= 0)
        {
            var w = Plain("OVERLAP", $"{Label(a)} overlaps {Label(b)}.", a);
            w.OtherEquipmentId = b.EquipmentId;
            warnings.Add(w);
            return;
        }

        var rule = GetRuleBothWays(a.HazardClass, b.HazardClass);
        if (rule == null || d >= rule.MinDistanceM) return;

        var warn = FromRule("CLEARANCE_PAIR",
            $"{Label(a)} to {Label(b)}: {d:F1} m, required {rule.MinDistanceM:F1} m.",
            a, rule, d);
        warn.OtherEquipmentId = b.EquipmentId;
        warnings.Add(warn);
    }

    private void CheckBoundary(LayoutInput input, PlacedItem item, List<Point2D> fp, List<LayoutWarning> warnings)
    {
        var rule = GetRuleBothWays(item.HazardClass, HazardClass.Boundary);
        if (rule == null || input.PlotCorners.Count < 3) return;

        double d = double.MaxValue;
        for (int i = 0; i < input.PlotCorners.Count; i++)
        {
            var p1 = input.PlotCorners[i];
            var p2 = input.PlotCorners[(i + 1) % input.PlotCorners.Count];
            for (int j = 0; j < fp.Count; j++)
            {
                double x = PolygonOps.DistanceBetweenSegments(p1, p2, fp[j], fp[(j + 1) % fp.Count]);
                if (x < d) d = x;
            }
        }

        if (d < rule.MinDistanceM)
        {
            warnings.Add(FromRule("CLEARANCE_BOUNDARY",
                $"{Label(item)} to plot boundary: {d:F1} m, required {rule.MinDistanceM:F1} m.",
                item, rule, d));
        }
    }

    private void CheckReceptors(LayoutInput input, PlacedItem item, List<Point2D> fp, List<LayoutWarning> warnings)
    {
        var rule = GetRuleBothWays(item.HazardClass, HazardClass.Receptor);
        if (rule == null) return;

        foreach (var r in input.Receptors)
        {
            double d = PolygonOps.Contains(fp, r.Position)
                ? 0
                : PolygonOps.DistanceToEdges(fp, r.Position);

            if (d < rule.MinDistanceM)
            {
                string name = string.IsNullOrWhiteSpace(r.Name) ? r.Type.ToString() : $"{r.Type} {r.Name}";
                warnings.Add(FromRule("CLEARANCE_RECEPTOR",
                    $"{Label(item)} to {name}: {d:F1} m, required {rule.MinDistanceM:F1} m.",
                    item, rule, d));
            }
        }
    }

    private void CheckRoads(IReadOnlyList<List<Point2D>> roads, PlacedItem item, List<Point2D> fp, List<LayoutWarning> warnings)
    {
        var rule = GetRuleBothWays(item.HazardClass, HazardClass.Road);
        if (rule == null) return;

        double d = double.MaxValue;
        foreach (var road in roads)
        {
            for (int i = 0; i < road.Count - 1; i++)
            {
                for (int j = 0; j < fp.Count; j++)
                {
                    double x = PolygonOps.DistanceBetweenSegments(
                        road[i], road[i + 1], fp[j], fp[(j + 1) % fp.Count]);
                    if (x < d) d = x;
                }
            }
        }

        if (d != double.MaxValue && d < rule.MinDistanceM)
        {
            warnings.Add(FromRule("CLEARANCE_ROAD",
                $"{Label(item)} to road: {d:F1} m, required {rule.MinDistanceM:F1} m.",
                item, rule, d));
        }
    }

    private ClearanceRuleValue? GetRuleBothWays(HazardClass a, HazardClass b)
        => _rules.GetRule(a, b) ?? _rules.GetRule(b, a);

    private static string Label(PlacedItem item)
        => item.InstanceNo > 1 ? $"{item.Tag} ({item.Name}) #{item.InstanceNo}" : $"{item.Tag} ({item.Name})";

    private static LayoutWarning Plain(string code, string message, PlacedItem item) => new()
    {
        Code = code,
        Message = message,
        EquipmentId = item.EquipmentId,
        InstanceNo = item.InstanceNo
    };

    private static LayoutWarning FromRule(string code, string message, PlacedItem item, ClearanceRuleValue rule, double actual) => new()
    {
        Code = code,
        Message = $"{message} [{rule.Status}] {rule.SourceNote}",
        RuleStatus = rule.Status,
        SourceNote = rule.SourceNote,
        EquipmentId = item.EquipmentId,
        InstanceNo = item.InstanceNo,
        ActualDistanceM = actual,
        RequiredDistanceM = rule.MinDistanceM
    };
}