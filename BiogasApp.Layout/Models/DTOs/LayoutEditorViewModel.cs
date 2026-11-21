using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class LayoutEditorViewModel
{
    public const string DraftNotice =
        "Draft layout for review by a qualified safety engineer. Not a compliance statement.";

    public static readonly string[] Zones = { "Dirty", "Process", "Gas", "Clean" };
    public static readonly string[] RuleStatuses = { "Official", "Reference", "Assumed" };

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }

    public SiteDto Site { get; set; } = new();
    public List<EquipmentItemDto> Equipment { get; set; } = new();

    public LayoutDto? ActiveLayout { get; set; }
    public List<LayoutDto> Versions { get; set; } = new();

    public Guid? RuleSetId { get; set; }
    public string RuleSetName { get; set; } = string.Empty;
    public int RuleSetVersion { get; set; }

    public bool IsPlotReady => Site.PlotCorners.Count >= 3;
    public bool HasLayout => ActiveLayout != null && ActiveLayout.Items.Count > 0;

    public int WarningCount => ActiveLayout?.Warnings.Count ?? 0;

    public int CountWarnings(string status) =>
        ActiveLayout?.Warnings.Count(w => w.RuleStatus == status) ?? 0;
}