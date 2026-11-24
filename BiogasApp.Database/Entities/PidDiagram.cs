using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class PidDiagram
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SimulationRunId { get; set; }
    public string DrawingNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int RevisionNo { get; set; } = 1;
    public PidApprovalStatus Status { get; set; } = PidApprovalStatus.Draft;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Project? Project { get; set; }
    public SimulationRun? SimulationRun { get; set; }
    public ICollection<PidEquipmentNode> Nodes { get; set; } = new List<PidEquipmentNode>();
    public ICollection<PidLine> Lines { get; set; } = new List<PidLine>();
    public ICollection<PidValve> Valves { get; set; } = new List<PidValve>();
    public ICollection<PidInstrument> Instruments { get; set; } = new List<PidInstrument>();
    public ICollection<PidRevision> Revisions { get; set; } = new List<PidRevision>();
}
