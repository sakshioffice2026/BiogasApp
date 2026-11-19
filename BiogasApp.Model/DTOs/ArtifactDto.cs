namespace BiogasApp.Model.DTOs;

public class ArtifactDto
{
    public Guid ArtifactId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ArtifactListDto
{
    public Guid SimulationRunId { get; set; }
    public List<ArtifactDto> Artifacts { get; set; } = new();
}