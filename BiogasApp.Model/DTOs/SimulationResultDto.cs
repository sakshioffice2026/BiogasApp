namespace BiogasApp.Model.DTOs;

public class SimulationResultDto
{
    public Guid SimulationRunId { get; set; }
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public MassBalanceDto MassBalance { get; set; } = new();
    public DigesterKineticsDto DigesterKinetics { get; set; } = new();
    public List<EquipmentSizingDto> EquipmentSizing { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}