using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class AssessmentNoBuildZone
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public NoBuildReason Reason { get; set; }
    public string PolygonJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public SiteAssessment? Assessment { get; set; }
}
