using BiogasApp.Database.Enums;

namespace BiogasApp.Model.ViewModel;

public class ProjectIndexViewModel
{
    public string? Search { get; set; }
    public ProjectStatus? StatusFilter { get; set; }
    public List<ProjectListItemViewModel> Projects { get; set; } = new();
}

public class ProjectListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = string.Empty;
    public RegulatoryStandard RegulatoryStandard { get; set; }
    public DigesterRegime DigesterRegime { get; set; }
    public UpgradingTech UpgradingTech { get; set; }
    public ProjectStatus Status { get; set; }
    public int RunCount { get; set; }
    public DateTime CreatedAt { get; set; }
}