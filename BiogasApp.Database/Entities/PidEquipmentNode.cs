using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidEquipmentNode
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public Guid? EquipmentItemId { get; set; }
    public Guid? SizedEquipmentId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PidSymbolType SymbolType { get; set; }
    public int StageNo { get; set; }
    public int InstanceNo { get; set; } = 1;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsLocked { get; set; }

    public PidDiagram? PidDiagram { get; set; }
    public ProjectEquipmentItem? EquipmentItem { get; set; }
    public SizedEquipment? SizedEquipment { get; set; }
}
