using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class SiteAssessment
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? EnquiryId { get; set; }
    public Guid? ProjectId { get; set; }
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public decimal CapacityTpd { get; set; } = 12m;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string PlotCornersJson { get; set; } = "[]";
    public decimal PlotAreaM2 { get; set; }
    public int GateEdgeIndex { get; set; }
    public decimal GateOffsetM { get; set; }
    public decimal NorthAngleDeg { get; set; }
    public decimal? WindFromDeg { get; set; }
    public decimal? SlopeDirectionDeg { get; set; }
    public decimal? PowerSideDeg { get; set; }
    public decimal? GasOfftakeSideDeg { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Enquiry? Enquiry { get; set; }
    public Project? Project { get; set; }
    public ICollection<AssessmentReceptor> Receptors { get; set; } = new List<AssessmentReceptor>();
    public ICollection<AssessmentNoBuildZone> NoBuildZones { get; set; } = new List<AssessmentNoBuildZone>();
    public ICollection<AssessmentEquipmentItem> Equipment { get; set; } = new List<AssessmentEquipmentItem>();
    public ICollection<AssessmentLayout> Layouts { get; set; } = new List<AssessmentLayout>();
}
