using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidValve
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public Guid PidLineId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public PidValveType ValveType { get; set; }
    public decimal NominalSizeMm { get; set; }
    public decimal PositionOnLine { get; set; } = 0.5m;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsNormallyOpen { get; set; } = true;
    public string? Notes { get; set; }

    public PidDiagram? PidDiagram { get; set; }
    public PidLine? PidLine { get; set; }
}
