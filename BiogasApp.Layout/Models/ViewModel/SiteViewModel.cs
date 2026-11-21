using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class SiteViewModel
{
    public const string DraftNotice =
        "Draft layout for review by a qualified safety engineer. Not a compliance statement.";

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }

    public SiteDto Site { get; set; } = new();
    public List<EquipmentItemDto> Equipment { get; set; } = new();

    public Guid? RuleSetId { get; set; }
    public string RuleSetName { get; set; } = string.Empty;
    public int RuleSetVersion { get; set; }

    public bool HasLocation => Site.Latitude != 0 || Site.Longitude != 0;
    public bool IsPlotReady => Site.PlotCorners.Count >= 3;
}
