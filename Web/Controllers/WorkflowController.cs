using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class WorkflowController : Controller
{
    private readonly IProjectRepository _projects;
    private readonly ISimulationRepository _simulations;
    private readonly ISiteRepository _sites;
    private readonly ISiteLayoutRepository _layouts;
    private readonly ICadGeneration _cad;

    public WorkflowController(
        IProjectRepository projects,
        ISimulationRepository simulations,
        ISiteRepository sites,
        ISiteLayoutRepository layouts,
        ICadGeneration cad)
    {
        _projects = projects;
        _simulations = simulations;
        _sites = sites;
        _layouts = layouts;
        _cad = cad;
    }

    [HttpGet]
    public async Task<IActionResult> Checklist(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var mix = await _projects.GetFeedstockMixAsync(projectId, cancellationToken);
        var run = await _simulations.GetLatestRunAsync(projectId, cancellationToken);
        var site = await _sites.GetByProjectAsync(projectId, cancellationToken);
        var equipment = await _sites.GetEquipmentAsync(projectId, cancellationToken);
        var layouts = await _layouts.GetVersionsAsync(projectId, cancellationToken);

        var artifacts = run == null
            ? new List<ArtifactDto>()
            : (await _cad.GetArtifactsAsync(run.Id, cancellationToken)).Artifacts;

        bool hasMix = mix.Count > 0;
        bool hasRun = run != null;
        bool hasPlot = site != null && site.PlotCorners.Count >= 3;
        int equipCount = equipment.Count(e => e.IsEnabled);
        bool hasEquipment = equipCount > 0;
        bool hasLayout = layouts.Count > 0;
        bool hasPfdFile = artifacts.Any(a => a.ArtifactType == "PFD_DXF");
        bool hasGaFile = artifacts.Any(a => a.ArtifactType == "GA_DXF");

        var steps = new List<ChecklistStep>
        {
            new()
            {
                Number = 1,
                Title = "Feedstock mix",
                Detail = hasMix
                    ? $"{mix.Count} feedstock(s), {mix.Sum(m => m.DailyTonnageTpd):0.#} TPD"
                    : "No feedstock mix saved yet.",
                State = hasMix ? ChecklistState.Done : ChecklistState.Pending,
                ActionText = hasMix ? "Edit" : "Open mixer",
                ActionUrl = Url.Action("Mixer", "Simulation", new { projectId })
            },
            new()
            {
                Number = 2,
                Title = "Simulation run",
                Detail = run != null
                    ? $"Run {run.RunVersion}, {run.NetCbgKgDay / 1000m:0.#} TPD CBG"
                    : "No simulation run yet.",
                State = hasRun ? ChecklistState.Done : ChecklistState.Pending,
                ActionText = hasRun ? "View results" : "Run simulation",
                ActionUrl = run != null
                    ? Url.Action("Results", "Simulation", new { runId = run.Id })
                    : Url.Action("Mixer", "Simulation", new { projectId })
            },
            new()
            {
                Number = 3,
                Title = "Site and plot",
                Detail = hasPlot
                    ? $"{site!.PlotCorners.Count} plot corners entered"
                    : "Plot corners not entered yet.",
                State = hasPlot ? ChecklistState.Done : ChecklistState.Pending,
                ActionText = hasPlot ? "Edit" : "Enter plot",
                ActionUrl = Url.Action("Edit", "Site", new { projectId })
            },
            BuildEquipmentStep(projectId, hasRun, hasEquipment, equipCount),
            new()
            {
                Number = 5,
                Title = "Layout version",
                Detail = hasLayout
                    ? $"{layouts.Count} saved version(s). Needed for the GA site plan only."
                    : "No saved layout. Needed for the GA site plan only.",
                State = hasLayout ? ChecklistState.Done : ChecklistState.Pending,
                ActionText = hasLayout ? "Open layout editor" : "Create layout",
                ActionUrl = Url.Action("Editor", "Layout", new { projectId })
            },
            new()
            {
                Number = 6,
                Title = "Drawings",
                Detail = hasPfdFile || hasGaFile
                    ? "Drawing files generated."
                    : "No drawing files yet.",
                State = hasPfdFile || hasGaFile ? ChecklistState.Done : ChecklistState.Pending
            }
        };

        bool currentFound = false;
        foreach (var step in steps)
        {
            if (step.State == ChecklistState.Done)
            {
                continue;
            }

            if (!currentFound)
            {
                step.State = ChecklistState.Current;
                currentFound = true;
            }
        }

        var model = new ProjectChecklistViewModel
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            LatestRunId = run?.Id,
            Steps = steps,
            HasPfdFile = hasPfdFile,
            HasGaFile = hasGaFile,
            PfdReady = hasRun && hasPlot && hasEquipment,
            PfdNote = !hasRun ? "Needs a simulation run"
                : !hasPlot ? "Needs the plot"
                : !hasEquipment ? "Needs equipment"
                : "Standard CBG flow order",
            GaReady = hasRun && hasLayout,
            GaNote = !hasRun ? "Needs a simulation run"
                : !hasLayout ? "Needs a saved layout"
                : "Uses the saved layout",
            GenerateUrl = run == null ? null : Url.Action("Index", "Cad", new { runId = run.Id }),
            PfdViewerUrl = run == null ? null : Url.Action("Viewer", "Cad", new { runId = run.Id, tab = "PFD Flow Sheet" }),
            GaViewerUrl = run == null ? null : Url.Action("Viewer", "Cad", new { runId = run.Id, tab = "GA Site Plan" })
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportEquipment(Guid projectId, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetLatestRunAsync(projectId, cancellationToken);
        if (run == null)
        {
            TempData["Error"] = "Run a simulation first.";
            return RedirectToAction(nameof(Checklist), new { projectId });
        }

        var count = await _sites.ImportEquipmentFromRunAsync(projectId, run.Id, cancellationToken);
        TempData["Success"] = count > 0
            ? $"{count} equipment item(s) imported from the latest run."
            : "Equipment is already up to date.";

        return RedirectToAction(nameof(Checklist), new { projectId });
    }

    private ChecklistStep BuildEquipmentStep(Guid projectId, bool hasRun, bool hasEquipment, int equipCount)
    {
        var step = new ChecklistStep
        {
            Number = 4,
            Title = "Equipment list",
            State = hasEquipment ? ChecklistState.Done : ChecklistState.Pending
        };

        if (hasEquipment)
        {
            step.Detail = $"{equipCount} item(s) in the equipment list.";
            step.ActionText = "Review equipment";
            step.ActionUrl = Url.Action("Editor", "Layout", new { projectId });
        }
        else if (hasRun)
        {
            step.Detail = "Import the sized equipment from the latest simulation run.";
            step.ActionText = "Import equipment";
            step.ActionUrl = Url.Action("ImportEquipment", "Workflow", new { projectId });
            step.ActionIsPost = true;
        }
        else
        {
            step.Detail = "Run a simulation first.";
        }

        return step;
    }
}
