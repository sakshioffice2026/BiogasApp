using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Output;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class SiteController : Controller
{
    private const double DefaultReceptorRadiusM = 500;   // odour; PESO 100 m is inside this
    private const double MaxReceptorRadiusM = 1000;
    private const long MaxDxfBytes = 20 * 1024 * 1024;

    private readonly ISiteRepository _sites;
    private readonly IProjectRepository _projects;
    private readonly ISimulationRepository _simulations;
    private readonly IClearanceRuleRepository _rules;
    private readonly IReceptorProvider _receptors;

    public SiteController(
        ISiteRepository sites,
        IProjectRepository projects,
        ISimulationRepository simulations,
        IClearanceRuleRepository rules,
        IReceptorProvider receptors)
    {
        _sites = sites;
        _projects = projects;
        _simulations = simulations;
        _rules = rules;
        _receptors = receptors;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project == null) return NotFound();

        var run = await _simulations.GetLatestRunAsync(projectId, cancellationToken);
        var ruleSet = await _rules.GetActiveRuleSetAsync(cancellationToken);
        var site = await _sites.GetByProjectAsync(projectId, cancellationToken)
                   ?? new SiteDto { ProjectId = projectId };

        var model = new LayoutEditorViewModel
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            SimulationRunId = run?.Id ?? Guid.Empty,
            Site = site,
            Equipment = await _sites.GetEquipmentAsync(projectId, cancellationToken),
            RuleSetId = ruleSet?.Id,
            RuleSetName = ruleSet?.Name ?? string.Empty,
            RuleSetVersion = ruleSet?.Version ?? 0
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SiteDto dto, CancellationToken cancellationToken)
    {
        if (dto == null || dto.ProjectId == Guid.Empty)
            return BadRequest(new { success = false, errors = new[] { "Project is missing." } });

        var project = await _projects.GetByIdAsync(dto.ProjectId, cancellationToken);
        if (project == null) return NotFound();

        var saved = await _sites.SaveAsync(dto, cancellationToken);

        var poly = saved.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList();
        var errors = PlotValidator.Validate(poly);

        return Json(new { success = errors.Count == 0, site = saved, errors });
    }

    [HttpGet]
    public async Task<IActionResult> FindReceptors(
        double lat,
        double lon,
        double radius = DefaultReceptorRadiusM,
        CancellationToken cancellationToken = default)
    {
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            return BadRequest(new { success = false, message = "Location is not valid." });

        radius = Math.Clamp(radius, 50, MaxReceptorRadiusM);

        var list = await _receptors.FindNearbyAsync(lat, lon, radius, cancellationToken);
        return Json(new { success = true, receptors = list });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportDxf(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Choose a DXF file." });

        if (file.Length > MaxDxfBytes)
            return BadRequest(new { success = false, message = "DXF file is larger than 20 MB." });

        if (!Path.GetExtension(file.FileName).Equals(".dxf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Only .dxf files are supported." });

        using var stream = file.OpenReadStream();
        var result = DxfPlotImporter.ImportBoundary(stream);

        return Json(new
        {
            success = result.Success,
            message = result.Message,
            scaleApplied = result.ScaleApplied,
            latitude = result.Latitude,
            longitude = result.Longitude,
            corners = result.Corners.Select(c => new PointDto { X = c.X, Y = c.Y })
        });
    }
}