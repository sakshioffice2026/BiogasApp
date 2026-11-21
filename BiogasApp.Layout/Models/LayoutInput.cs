using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;

namespace BiogasApp.Layout.Models;

public sealed class LayoutInput
{
    public List<Point2D> PlotCorners { get; set; } = new();
    public int GateEdgeIndex { get; set; }
    public double GateOffsetM { get; set; }
    public double NorthAngleDeg { get; set; }
    public double? WindFromDeg { get; set; }
    public double? SlopeDirectionDeg { get; set; }
    public double? PowerSideDeg { get; set; }
    public double? GasOfftakeSideDeg { get; set; }
    public List<EquipmentInput> Equipment { get; set; } = new();
    public List<NoBuildInput> NoBuildZones { get; set; } = new();
    public List<ReceptorInput> Receptors { get; set; } = new();
    public List<LockedPlacement> LockedItems { get; set; } = new();
}

public sealed record EquipmentInput(
    Guid Id, string Tag, string Name,
    HazardClass HazardClass, EquipmentZone Zone, EquipmentShape Shape,
    double WidthM, double LengthM, double DiameterM,
    int Quantity, bool AllowRotation, bool NeedsRoadAccess);

public sealed record NoBuildInput(NoBuildReason Reason, List<Point2D> Polygon, string? Notes);

public sealed record ReceptorInput(ReceptorType Type, string? Name, Point2D Position, double DistanceFromPlotM);

public sealed record LockedPlacement(Guid EquipmentId, int InstanceNo, double X, double Y, double RotationDeg);