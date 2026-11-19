namespace BiogasApp.Model.DTOs;

public class CadJobStatusDto
{
    public Guid JobId { get; set; }
    public Guid SimulationRunId { get; set; }
    public string Status { get; set; } = "QUEUED";
    public int EstimatedTimeSeconds { get; set; }
    public string StatusPollUrl { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}