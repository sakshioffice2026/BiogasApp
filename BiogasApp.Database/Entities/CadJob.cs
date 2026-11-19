using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class CadJob
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public CadJobStatus Status { get; set; } = CadJobStatus.Queued;
    public string ExportFormatsJson { get; set; } = "[]";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public SimulationRun? SimulationRun { get; set; }
}