using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class PidEditorViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }
    public Guid? PidDiagramId { get; set; }
    public PidDiagramDto Diagram { get; set; } = new();
    public List<PidRunOptionDto> AvailableRuns { get; set; } = new();
    public List<PidRevisionItemDto> Revisions { get; set; } = new();
    public bool IsReadOnly { get; set; }
}

public class PidRunOptionDto
{
    public Guid SimulationRunId { get; set; }
    public int RunVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PidRevisionItemDto
{
    public Guid Id { get; set; }
    public int RevisionNo { get; set; }
    public PidApprovalStatus Status { get; set; }
    public string? Remarks { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}
