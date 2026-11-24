using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class ProjectController : Controller
{
    private readonly IProjectRepository _projects;
    private readonly ISimulationRepository _simulations;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(IProjectRepository projects, ISimulationRepository simulations, ILogger<ProjectController> logger)
    {
        _projects = projects;
        _simulations = simulations;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, ProjectStatus? status, CancellationToken cancellationToken)
    {
        var model = await _projects.GetIndexAsync(search, status, cancellationToken);

        var latestRuns = new Dictionary<Guid, Guid?>();
        foreach (var p in model.Projects)
        {
            var run = await _simulations.GetLatestRunAsync(p.Id, cancellationToken);
            latestRuns[p.Id] = run?.Id;
        }

        ViewData["LatestRuns"] = latestRuns;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, bool details = false, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var latestRun = await _simulations.GetLatestRunAsync(id, cancellationToken);
        if (!details && latestRun != null)
        {
            return RedirectToAction("Results", "Simulation", new { runId = latestRun.Id });
        }

        ViewData["LatestRunId"] = latestRun?.Id;

        project.FeedstockMix = await _projects.GetFeedstockMixAsync(id, cancellationToken);
        return View(project);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ProjectFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            LocationCountry = model.LocationCountry.Trim(),
            RegulatoryStandard = model.RegulatoryStandard,
            AmbientTempMinC = model.AmbientTempMinC,
            AmbientTempMaxC = model.AmbientTempMaxC,
            DigesterRegime = model.DigesterRegime,
            UpgradingTech = model.UpgradingTech,
            Status = ProjectStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _projects.AddAsync(project, cancellationToken);
        TempData["Success"] = "Project created. Next: Step 2 of 7, blend your feedstock.";
        return RedirectToAction("Mixer", "Simulation", new { projectId = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var model = new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            LocationCountry = project.LocationCountry,
            RegulatoryStandard = project.RegulatoryStandard,
            AmbientTempMinC = project.AmbientTempMinC,
            AmbientTempMaxC = project.AmbientTempMaxC,
            DigesterRegime = project.DigesterRegime,
            UpgradingTech = project.UpgradingTech,
            Status = project.Status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        project.Name = model.Name.Trim();
        project.LocationCountry = model.LocationCountry.Trim();
        project.RegulatoryStandard = model.RegulatoryStandard;
        project.AmbientTempMinC = model.AmbientTempMinC;
        project.AmbientTempMaxC = model.AmbientTempMaxC;
        project.DigesterRegime = model.DigesterRegime;
        project.UpgradingTech = model.UpgradingTech;
        project.Status = model.Status;

        await _projects.UpdateAsync(project, cancellationToken);
        TempData["Success"] = "Project updated.";
        return RedirectToAction(nameof(Detail), new { id, details = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, ProjectStatus status, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        await _projects.SetStatusAsync(id, status, cancellationToken);
        TempData["Success"] = "Project status updated.";
        return RedirectToAction(nameof(Detail), new { id, details = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFeedstockMix(
        Guid id,
        List<ProjectFeedstockMix> mix,
        CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var cleaned = (mix ?? new List<ProjectFeedstockMix>())
            .Where(m => m.FeedstockId != Guid.Empty && m.DailyTonnageTpd > 0)
            .Select(m => new ProjectFeedstockMix
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                FeedstockId = m.FeedstockId,
                DailyTonnageTpd = m.DailyTonnageTpd,
                SeasonalAvailabilityDays = m.SeasonalAvailabilityDays <= 0 ? 365 : m.SeasonalAvailabilityDays,
                CustomTsOverride = m.CustomTsOverride,
                CustomVsOverride = m.CustomVsOverride
            })
            .ToList();

        await _projects.SaveFeedstockMixAsync(id, cleaned, cancellationToken);
        TempData["Success"] = "Feedstock mix saved.";
        return RedirectToAction(nameof(Detail), new { id, details = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _projects.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Project deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {ProjectId}", id);
            TempData["Error"] = "Project could not be deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}