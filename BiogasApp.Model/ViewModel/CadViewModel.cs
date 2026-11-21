using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class CadViewModel
{
    public static readonly string[] AvailableFormats =
    {
        "GA_DXF", "PFD_DXF", "SHELL_DEV_DXF", "MODEL_3D_STEP"
    };

    public static readonly string[] ViewerTabs =
    {
        "GA Site Plan", "PFD Flow Sheet", "Shell Plate Unrolling", "Nozzle Schedule"
    };

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }

    public List<string> SelectedFormats { get; set; } = new(AvailableFormats);
    public DxfOptionsDto DxfOptions { get; set; } = new();

    public string ActiveTab { get; set; } = "GA Site Plan";
    public bool ShowDigesters { get; set; } = true;
    public bool ShowPiping { get; set; } = true;
    public bool ShowHazardArcs { get; set; } = true;

    public CadJobStatusDto? CurrentJob { get; set; }
    public List<ArtifactDto> Artifacts { get; set; } = new();

    public Guid? SiteLayoutId { get; set; }
    public List<LayoutDto> Layouts { get; set; } = new();
}