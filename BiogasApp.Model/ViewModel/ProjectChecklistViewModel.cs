namespace BiogasApp.Model.ViewModel;

public enum ChecklistState
{
    Done,
    Current,
    Pending
}

public class ChecklistStep
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public ChecklistState State { get; set; } = ChecklistState.Pending;
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public bool ActionIsPost { get; set; }
}

public class ProjectChecklistViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? LatestRunId { get; set; }

    public List<ChecklistStep> Steps { get; set; } = new();

    public bool PfdReady { get; set; }
    public string PfdNote { get; set; } = string.Empty;
    public bool HasPfdFile { get; set; }

    public bool GaReady { get; set; }
    public string GaNote { get; set; } = string.Empty;
    public bool HasGaFile { get; set; }

    public string? GenerateUrl { get; set; }
    public string? PfdViewerUrl { get; set; }
    public string? GaViewerUrl { get; set; }

    public int DoneCount => Steps.Count(s => s.State == ChecklistState.Done);

    public int ProgressPercent => Steps.Count == 0 ? 0 : (int)Math.Round(DoneCount * 100.0 / Steps.Count);
}
