using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidInstrument
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public Guid? PidLineId { get; set; }
    public Guid? PidEquipmentNodeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public PidInstrumentType InstrumentType { get; set; }
    public string? Service { get; set; }
    public decimal? RangeMin { get; set; }
    public decimal? RangeMax { get; set; }
    public string? Unit { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string? Notes { get; set; }

    public PidDiagram? PidDiagram { get; set; }
    public PidLine? PidLine { get; set; }
    public PidEquipmentNode? PidEquipmentNode { get; set; }
}
