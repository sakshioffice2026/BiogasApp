using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidRevision
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public int RevisionNo { get; set; }
    public PidApprovalStatus Status { get; set; } = PidApprovalStatus.Draft;
    public string? Remarks { get; set; }
    public string? SnapshotJson { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public PidDiagram? PidDiagram { get; set; }
}
