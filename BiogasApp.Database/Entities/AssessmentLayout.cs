using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class AssessmentLayout
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int VersionNo { get; set; }
    public LayoutOrigin Origin { get; set; }
    public Guid? RuleSetId { get; set; }
    public int? RuleSetVersion { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SiteAssessment? Assessment { get; set; }
    public ClearanceRuleSet? RuleSet { get; set; }
    public ICollection<AssessmentLayoutItem> Items { get; set; } = new List<AssessmentLayoutItem>();
}
