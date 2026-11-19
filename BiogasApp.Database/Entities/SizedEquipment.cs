namespace BiogasApp.Database.Entities;

public class SizedEquipment
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public string EquipmentTag { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? GeometryParamsJson { get; set; }
    public string ProcessDutyJson { get; set; } = "{}";
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public SimulationRun? SimulationRun { get; set; }
}