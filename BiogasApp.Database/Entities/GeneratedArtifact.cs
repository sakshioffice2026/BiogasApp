using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class GeneratedArtifact
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public ArtifactType ArtifactType { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SimulationRun? SimulationRun { get; set; }
}