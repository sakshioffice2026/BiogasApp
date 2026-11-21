namespace BiogasApp.Layout.Geometry;

public static class PolygonOps
{
    private const double Eps = 1e-9;

    public static double SignedArea(IReadOnlyList<Point2D> poly)
    {
        double sum = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            sum += a.Cross(b);
        }
        return sum / 2.0;
    }

    public static double Area(IReadOnlyList<Point2D> poly) => Math.Abs(SignedArea(poly));

    public static bool IsCounterClockwise(IReadOnlyList<Point2D> poly) => SignedArea(poly) > 0;

    public static List<Point2D> EnsureCounterClockwise(IReadOnlyList<Point2D> poly)
    {
        var list = poly.ToList();
        if (!IsCounterClockwise(list)) list.Reverse();
        return list;
    }

    public static double Perimeter(IReadOnlyList<Point2D> poly)
    {
        double sum = 0;
        for (int i = 0; i < poly.Count; i++)
            sum += poly[i].DistanceTo(poly[(i + 1) % poly.Count]);
        return sum;
    }

    public static Point2D Centroid(IReadOnlyList<Point2D> poly)
    {
        double a = SignedArea(poly);
        if (Math.Abs(a) < Eps)
        {
            return new Point2D(poly.Average(p => p.X), poly.Average(p => p.Y));
        }
        double cx = 0, cy = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % poly.Count];
            double f = p.Cross(q);
            cx += (p.X + q.X) * f;
            cy += (p.Y + q.Y) * f;
        }
        return new Point2D(cx / (6 * a), cy / (6 * a));
    }

    public static (Point2D Min, Point2D Max) BoundingBox(IReadOnlyList<Point2D> poly)
    {
        return (
            new Point2D(poly.Min(p => p.X), poly.Min(p => p.Y)),
            new Point2D(poly.Max(p => p.X), poly.Max(p => p.Y)));
    }

    public static List<Point2D> RectCorners(Point2D center, double width, double height, double rotationRadians = 0)
    {
        double hw = width / 2.0, hh = height / 2.0;
        var local = new[]
        {
            new Point2D(-hw, -hh),
            new Point2D(hw, -hh),
            new Point2D(hw, hh),
            new Point2D(-hw, hh)
        };
        return local.Select(p => p.Rotate(rotationRadians) + center).ToList();
    }

    public static bool Contains(IReadOnlyList<Point2D> poly, Point2D pt)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            bool crosses = (a.Y > pt.Y) != (b.Y > pt.Y);
            if (crosses)
            {
                double xAt = (b.X - a.X) * (pt.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (pt.X < xAt) inside = !inside;
            }
        }
        return inside;
    }

    public static double DistanceToSegment(Point2D p, Point2D a, Point2D b)
    {
        var ab = b - a;
        double len2 = ab.Dot(ab);
        if (len2 < Eps) return p.DistanceTo(a);
        double t = Math.Clamp((p - a).Dot(ab) / len2, 0, 1);
        var proj = a + ab * t;
        return p.DistanceTo(proj);
    }

    public static double DistanceToEdges(IReadOnlyList<Point2D> poly, Point2D p)
    {
        double best = double.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            double d = DistanceToSegment(p, poly[i], poly[(i + 1) % poly.Count]);
            if (d < best) best = d;
        }
        return best;
    }

    public static bool SegmentsIntersect(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = (a2 - a1).Cross(b1 - a1);
        double d2 = (a2 - a1).Cross(b2 - a1);
        double d3 = (b2 - b1).Cross(a1 - b1);
        double d4 = (b2 - b1).Cross(a2 - b1);

        if (((d1 > Eps && d2 < -Eps) || (d1 < -Eps && d2 > Eps)) &&
            ((d3 > Eps && d4 < -Eps) || (d3 < -Eps && d4 > Eps)))
            return true;

        if (Math.Abs(d1) <= Eps && OnSegment(a1, a2, b1)) return true;
        if (Math.Abs(d2) <= Eps && OnSegment(a1, a2, b2)) return true;
        if (Math.Abs(d3) <= Eps && OnSegment(b1, b2, a1)) return true;
        if (Math.Abs(d4) <= Eps && OnSegment(b1, b2, a2)) return true;
        return false;
    }

    private static bool OnSegment(Point2D a, Point2D b, Point2D p)
    {
        return p.X >= Math.Min(a.X, b.X) - Eps && p.X <= Math.Max(a.X, b.X) + Eps &&
               p.Y >= Math.Min(a.Y, b.Y) - Eps && p.Y <= Math.Max(a.Y, b.Y) + Eps;
    }

    public static double DistanceBetweenSegments(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        if (SegmentsIntersect(a1, a2, b1, b2)) return 0;
        return Math.Min(
            Math.Min(DistanceToSegment(a1, b1, b2), DistanceToSegment(a2, b1, b2)),
            Math.Min(DistanceToSegment(b1, a1, a2), DistanceToSegment(b2, a1, a2)));
    }

    public static bool IsSelfIntersecting(IReadOnlyList<Point2D> poly)
    {
        int n = poly.Count;
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
        {
            var a1 = poly[i];
            var a2 = poly[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                if (j == i) continue;
                if ((j + 1) % n == i || (i + 1) % n == j) continue;
                var b1 = poly[j];
                var b2 = poly[(j + 1) % n];
                if (SegmentsIntersect(a1, a2, b1, b2)) return true;
            }
        }
        return false;
    }

    public static bool Overlaps(IReadOnlyList<Point2D> a, IReadOnlyList<Point2D> b)
    {
        for (int i = 0; i < a.Count; i++)
        {
            var a1 = a[i];
            var a2 = a[(i + 1) % a.Count];
            for (int j = 0; j < b.Count; j++)
            {
                if (SegmentsIntersect(a1, a2, b[j], b[(j + 1) % b.Count])) return true;
            }
        }
        return Contains(b, a[0]) || Contains(a, b[0]);
    }

    public static double DistanceBetween(IReadOnlyList<Point2D> a, IReadOnlyList<Point2D> b)
    {
        if (Overlaps(a, b)) return 0;
        double best = double.MaxValue;
        for (int i = 0; i < a.Count; i++)
        {
            var a1 = a[i];
            var a2 = a[(i + 1) % a.Count];
            for (int j = 0; j < b.Count; j++)
            {
                double d = DistanceBetweenSegments(a1, a2, b[j], b[(j + 1) % b.Count]);
                if (d < best) best = d;
            }
        }
        return best;
    }

    public static bool ContainsPolygon(IReadOnlyList<Point2D> outer, IReadOnlyList<Point2D> inner)
    {
        foreach (var p in inner)
            if (!Contains(outer, p)) return false;

        for (int i = 0; i < inner.Count; i++)
        {
            var a1 = inner[i];
            var a2 = inner[(i + 1) % inner.Count];
            for (int j = 0; j < outer.Count; j++)
            {
                var b1 = outer[j];
                var b2 = outer[(j + 1) % outer.Count];
                if (ProperCross(a1, a2, b1, b2)) return false;
            }
        }
        return true;
    }

    private static bool ProperCross(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = (a2 - a1).Cross(b1 - a1);
        double d2 = (a2 - a1).Cross(b2 - a1);
        double d3 = (b2 - b1).Cross(a1 - b1);
        double d4 = (b2 - b1).Cross(a2 - b1);
        return ((d1 > Eps && d2 < -Eps) || (d1 < -Eps && d2 > Eps)) &&
               ((d3 > Eps && d4 < -Eps) || (d3 < -Eps && d4 > Eps));
    }
}