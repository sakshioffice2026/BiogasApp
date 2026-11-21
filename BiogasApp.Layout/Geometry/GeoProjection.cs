namespace BiogasApp.Layout.Geometry;

public sealed class GeoProjection
{
    private const double EarthRadius = 6371008.8;
    private const double Deg2Rad = Math.PI / 180.0;
    private readonly double _cosLat;

    public double OriginLat { get; }
    public double OriginLon { get; }

    public GeoProjection(double originLat, double originLon)
    {
        OriginLat = originLat;
        OriginLon = originLon;
        _cosLat = Math.Cos(originLat * Deg2Rad);
    }

    public Point2D ToMetres(double lat, double lon)
    {
        double x = (lon - OriginLon) * Deg2Rad * EarthRadius * _cosLat;
        double y = (lat - OriginLat) * Deg2Rad * EarthRadius;
        return new Point2D(x, y);
    }

    public (double Lat, double Lon) ToLatLon(Point2D p)
    {
        double lat = OriginLat + p.Y / EarthRadius / Deg2Rad;
        double lon = OriginLon + p.X / (EarthRadius * _cosLat) / Deg2Rad;
        return (lat, lon);
    }

    public static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Deg2Rad;
        double dLon = (lon2 - lon1) * Deg2Rad;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Deg2Rad) * Math.Cos(lat2 * Deg2Rad) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}