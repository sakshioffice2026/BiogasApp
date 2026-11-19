using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = string.Empty;
    public RegulatoryStandard RegulatoryStandard { get; set; }
    public decimal AmbientTempMinC { get; set; }
    public decimal AmbientTempMaxC { get; set; }
    public DigesterRegime DigesterRegime { get; set; }
    public UpgradingTech UpgradingTech { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectFeedstockMix> FeedstockMix { get; set; } = new List<ProjectFeedstockMix>();
    public ICollection<SimulationRun> SimulationRuns { get; set; } = new List<SimulationRun>();
}