namespace BiogasApp.Model.DTOs;

public class EquipmentSizingDto
{
    public string EquipmentTag { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Dictionary<string, object?> GeometryParams { get; set; } = new();
    public Dictionary<string, object?> ProcessDuty { get; set; } = new();
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}