using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidLine
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public string LineNo { get; set; } = string.Empty;
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public PidMedium Medium { get; set; }
    public decimal NominalSizeMm { get; set; }
    public string? Material { get; set; }
    public bool IsInsulated { get; set; }
    public string PointsJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public PidDiagram? PidDiagram { get; set; }
    public PidEquipmentNode? FromNode { get; set; }
    public PidEquipmentNode? ToNode { get; set; }
}
