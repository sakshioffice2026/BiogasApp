using BiogasApp.Database;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Web.Controllers;

public class CadController : Controller
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = BuildContentTypes();

    private readonly ICadGeneration _cad;
    private readonly ISimulationRepository _simulations;
    private readonly IProjectRepository _projects;
    private readonly IArtifactStorage _storage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CadController> _logger;

    public CadController(
        ICadGeneration cad,
        ISimulationRepository simulations,
        IProjectRepository projects,
        IArtifactStorage storage,
        ApplicationDbContext db,
        ILogger<CadController> logger)
    {
        _cad = cad;
        _simulations = simulations;
        _projects = projects;
        _storage = storage;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid runId, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(runId, null, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Viewer(Guid runId, string? tab, CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(runId, tab, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(CadViewModel model, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(model.SimulationRunId, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }

        var formats = (model.SelectedFormats ?? new List<string>())
            .Where(f => CadViewModel.AvailableFormats.Contains(f))
            .Distinct()
            .ToList();

        if (formats.Count == 0)
        {
            TempData["Error"] = "Select at least one export format.";
            return RedirectToAction(nameof(Index), new { runId = model.SimulationRunId });
        }

        try
        {
            var status = await _cad.QueueJobAsync(new CadJobRequestDto
            {
                SimulationRunId = model.SimulationRunId,
                ExportFormats = formats,
                DxfOptions = model.DxfOptions ?? new DxfOptionsDto()
            }, cancellationToken);

            if (IsAjax())
            {
                return Json(status);
            }

            TempData["Success"] = "CAD generation queued.";
            return RedirectToAction(nameof(Index), new { runId = model.SimulationRunId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue CAD job for run {RunId}", model.SimulationRunId);

            if (IsAjax())
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "CAD service is unavailable." });
            }

            TempData["Error"] = "CAD service is unavailable. Try again later.";
            return RedirectToAction(nameof(Index), new { runId = model.SimulationRunId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        var status = await _cad.GetJobStatusAsync(id, cancellationToken);
        if (status == null)
        {
            return NotFound();
        }

        return Json(status);
    }

    [HttpGet]
    public async Task<IActionResult> Artifacts(Guid runId, CancellationToken cancellationToken)
    {
        var list = await _cad.GetArtifactsAsync(runId, cancellationToken);
        return Json(list);
    }

    [HttpGet]
    public async Task<IActionResult> History(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }

        var jobs = await _cad.GetJobsByRunAsync(runId, cancellationToken);

        ViewData["SimulationRunId"] = runId;
        ViewData["ProjectId"] = run.ProjectId;
        return View(jobs.OrderByDescending(j => j.CreatedAt).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var artifact = await _db.GeneratedArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (artifact == null)
        {
            return NotFound();
        }

        var stream = await _storage.OpenReadAsync(artifact.FileUrl, cancellationToken);
        if (stream == null)
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(artifact.FileUrl);
        if (!ContentTypes.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return File(stream, contentType, fileName);
    }

    private async Task<CadViewModel?> BuildViewModelAsync(Guid runId, string? tab, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken);
        if (run == null)
        {
            return null;
        }

        var project = await _projects.GetByIdAsync(run.ProjectId, cancellationToken);
        if (project == null)
        {
            return null;
        }

        var artifacts = await _cad.GetArtifactsAsync(runId, cancellationToken);
        var jobs = await _cad.GetJobsByRunAsync(runId, cancellationToken);
        var latestJob = jobs.OrderByDescending(j => j.CreatedAt).FirstOrDefault();

        CadJobStatusDto? current = null;
        if (latestJob != null)
        {
            current = await _cad.GetJobStatusAsync(latestJob.Id, cancellationToken);
        }

        return new CadViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            SimulationRunId = runId,
            ActiveTab = !string.IsNullOrWhiteSpace(tab) && CadViewModel.ViewerTabs.Contains(tab)
                ? tab
                : CadViewModel.ViewerTabs[0],
            CurrentJob = current,
            Artifacts = artifacts.Artifacts
        };
    }

    private bool IsAjax() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private static FileExtensionContentTypeProvider BuildContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".dxf"] = "application/dxf";
        provider.Mappings[".step"] = "application/step";
        provider.Mappings[".stp"] = "application/step";
        provider.Mappings[".glb"] = "model/gltf-binary";
        provider.Mappings[".gltf"] = "model/gltf+json";
        return provider;
    }
}