using BiogasApp.Layout.Geometry;
using netDxf;

namespace BiogasApp.Layout.Output;

public sealed record PlotImportResult(
    bool Success,
    List<Point2D> Corners,
    double ScaleApplied,
    string Message,
    double? Latitude = null,
    double? Longitude = null);
public static class DxfPlotImporter
{
    private const double MmThresholdM = 5000.0;
    private const double ClosedToleranceM = 0.01;

    public static PlotImportResult ImportBoundary(Stream stream)
    {
        DxfDocument? doc;
        try
        {
            doc = DxfDocument.Load(stream);
        }
        catch (Exception ex)
        {
            return Fail($"DXF could not be read: {ex.Message}");
        }

        if (doc == null) return Fail("DXF could not be read.");

        var candidates = new List<List<Point2D>>();

        foreach (var pl in doc.Entities.Polylines2D)
        {
            var pts = pl.Vertexes.Select(v => new Point2D(v.Position.X, v.Position.Y)).ToList();
            if (IsClosedShape(pts, pl.IsClosed)) candidates.Add(pts);
        }

        foreach (var pl in doc.Entities.Polylines3D)
        {
            var pts = pl.Vertexes.Select(v => new Point2D(v.X, v.Y)).ToList();
            if (IsClosedShape(pts, pl.IsClosed)) candidates.Add(pts);
        }

        if (candidates.Count == 0)
            return Fail("No closed polyline found in the DXF. Draw the plot boundary as one closed polyline.");

        var best = candidates
            .Select(Clean)
            .Where(c => c.Count >= 3)
            .OrderByDescending(PolygonOps.Area)
            .FirstOrDefault();

        if (best == null) return Fail("Boundary has fewer than 3 distinct corners.");

        var (min, max) = PolygonOps.BoundingBox(best);
        double extent = Math.Max(max.X - min.X, max.Y - min.Y);

        bool geographic = extent > 0 && extent <= 1.0
            && min.X >= -180 && max.X <= 180 && min.Y >= -90 && max.Y <= 90;

        if (geographic)
        {
            var proj = new GeoProjection(min.Y, min.X);
            var geo = PolygonOps.EnsureCounterClockwise(best.Select(p => proj.ToMetres(p.Y, p.X)).ToList());
            return new PlotImportResult(true, geo, 1.0,
                "DXF had map coordinates: boundary and plot location imported. Please check the area.", min.Y, min.X);
        }

        double scale = extent > MmThresholdM ? 0.001 : 1.0;

        var corners = best
            .Select(p => new Point2D((p.X - min.X) * scale, (p.Y - min.Y) * scale))
            .ToList();
        corners = PolygonOps.EnsureCounterClockwise(corners);

        string note = scale == 1.0
            ? "Boundary imported in metres."
            : "Boundary looked like millimetres and was converted to metres. Please check the area.";

        return new PlotImportResult(true, corners, scale, note);
    }

    private static bool IsClosedShape(List<Point2D> pts, bool flagClosed)
    {
        if (pts.Count < 3) return false;
        if (flagClosed) return true;
        return pts[0].DistanceTo(pts[^1]) <= ClosedToleranceM;
    }

    private static List<Point2D> Clean(List<Point2D> pts)
    {
        var list = new List<Point2D>();
        foreach (var p in pts)
        {
            if (list.Count == 0 || list[^1].DistanceTo(p) > ClosedToleranceM) list.Add(p);
        }
        if (list.Count > 1 && list[0].DistanceTo(list[^1]) <= ClosedToleranceM)
            list.RemoveAt(list.Count - 1);
        return list;
    }

    private static PlotImportResult Fail(string message)
        => new(false, new List<Point2D>(), 1.0, message);
}