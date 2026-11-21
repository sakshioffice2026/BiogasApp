namespace BiogasApp.Layout.Geometry;

public readonly record struct Point2D(double X, double Y)
{
    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator *(Point2D a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public double DistanceTo(Point2D other) => (this - other).Length;

    public double Dot(Point2D other) => X * other.X + Y * other.Y;

    public double Cross(Point2D other) => X * other.Y - Y * other.X;

    public Point2D Rotate(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new Point2D(X * c - Y * s, X * s + Y * c);
    }
}