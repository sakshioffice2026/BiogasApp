using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;

namespace BiogasApp.Layout.Models;

public sealed class PlacedItem
{
    public Guid EquipmentId { get; set; }
    public int InstanceNo { get; set; } = 1;
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HazardClass HazardClass { get; set; }
    public EquipmentZone Zone { get; set; }
    public EquipmentShape Shape { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double WidthM { get; set; }
    public double LengthM { get; set; }
    public double RotationDeg { get; set; }
    public bool IsLocked { get; set; }
    public bool NeedsRoadAccess { get; set; } = true;

    public Point2D Center => new(X, Y);

    public List<Point2D> GetFootprint()
    {
        if (Shape == EquipmentShape.Circle)
        {
            double r = WidthM / 2.0;
            const int segments = 24;
            var pts = new List<Point2D>(segments);
            for (int i = 0; i < segments; i++)
            {
                double a = 2 * Math.PI * i / segments;
                pts.Add(new Point2D(X + r * Math.Cos(a), Y + r * Math.Sin(a)));
            }
            return pts;
        }
        return PolygonOps.RectCorners(Center, WidthM, LengthM, RotationDeg * Math.PI / 180.0);
    }
}