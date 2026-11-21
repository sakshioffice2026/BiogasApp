using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class SiteLayout
{
    public Guid Id { get; set; }
    public Guid SimulationRunId { get; set; }
    public Guid ProjectId { get; set; }
    public int VersionNo { get; set; }
    public LayoutOrigin Origin { get; set; }
    public Guid? RuleSetId { get; set; }
    public int? RuleSetVersion { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SimulationRun? SimulationRun { get; set; }
    public Project? Project { get; set; }
    public ClearanceRuleSet? RuleSet { get; set; }
    public ICollection<SiteLayoutItem> Items { get; set; } = new List<SiteLayoutItem>();
}
