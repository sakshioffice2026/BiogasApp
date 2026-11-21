namespace BiogasApp.Layout.Geometry;

public static class PlotValidator
{
    public static List<string> Validate(IReadOnlyList<Point2D> plot, double minAreaSqm = 500)
    {
        var errors = new List<string>();

        if (plot == null || plot.Count < 3)
        {
            errors.Add("Plot needs at least 3 corner points.");
            return errors;
        }

        for (int i = 0; i < plot.Count; i++)
        {
            if (plot[i].DistanceTo(plot[(i + 1) % plot.Count]) < 0.01)
                errors.Add($"Corner {i + 1} and corner {(i + 1) % plot.Count + 1} are the same point.");
        }

        if (errors.Count > 0) return errors;

        if (PolygonOps.IsSelfIntersecting(plot))
            errors.Add("Plot boundary crosses itself.");

        double area = PolygonOps.Area(plot);
        if (area < minAreaSqm)
            errors.Add($"Plot area {area:F0} sq m is below the minimum {minAreaSqm:F0} sq m.");

        return errors;
    }

    public static bool IsValid(IReadOnlyList<Point2D> plot, double minAreaSqm = 500)
        => Validate(plot, minAreaSqm).Count == 0;
}