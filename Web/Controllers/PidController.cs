using System.Text.Json;
using System.Text.Json.Serialization;
using BiogasApp.Business.Pid;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class PidStatusRequest
{
    public Guid Id { get; set; }
    public PidApprovalStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public class PidRevisionRequest
{
    public Guid Id { get; set; }
    public string? Remarks { get; set; }
}

public class PidController : Controller
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly JsonSerializerOptions PidJson = CreateJson();

    private readonly IPidRepository _pid;
    private readonly IPidExport _export;
    private readonly ISiteRepository _sites;
    private readonly PidTemplateBuilder _template;
    private readonly PidAutoLayoutService _layout;

    public PidController(
        IPidRepository pid,
        IPidExport export,
        ISiteRepository sites,
        PidTemplateBuilder template,
        PidAutoLayoutService layout)
    {
        _pid = pid;
        _export = export;
        _sites = sites;
        _template = template;
        _layout = layout;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private async Task<T?> ReadBodyAsync<T>(CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(Request.Body, PidJson, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid projectId, Guid? runId, CancellationToken cancellationToken)
    {
        var projectName = await _pid.GetProjectNameAsync(projectId, cancellationToken);
        if (projectName == null) return NotFound();

        var runs = await _pid.GetRunOptionsAsync(projectId, cancellationToken);
        var selected = runId ?? runs.FirstOrDefault()?.SimulationRunId ?? Guid.Empty;
        var existing = selected == Guid.Empty
            ? null
            : await _pid.GetLatestAsync(projectId, selected, cancellationToken);

        var model = new PidEditorViewModel
        {
            ProjectId = projectId,
            ProjectName = projectName,
            SimulationRunId = selected,
            PidDiagramId = existing?.Id,
            Diagram = existing ?? new PidDiagramDto { ProjectId = projectId },
            AvailableRuns = runs,
            Revisions = existing == null
                ? new List<PidRevisionItemDto>()
                : await _pid.GetRevisionsAsync(existing.Id, cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(Guid projectId, Guid simulationRunId, CancellationToken cancellationToken)
    {
        var projectName = await _pid.GetProjectNameAsync(projectId, cancellationToken);
        if (projectName == null) return NotFound();

        var runs = await _pid.GetRunOptionsAsync(projectId, cancellationToken);
        if (!runs.Any(r => r.SimulationRunId == simulationRunId))
        {
            TempData["PidError"] = "Select a simulation run.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        await _sites.ImportEquipmentFromRunAsync(projectId, simulationRunId, cancellationToken);

        var items = await _pid.GetProjectEquipmentAsync(projectId, cancellationToken);
        var sized = await _pid.GetSizedEquipmentAsync(simulationRunId, cancellationToken);

        var diagram = _template.Build(projectId, simulationRunId, items, sized, User?.Identity?.Name);
        if (diagram.Nodes.Count == 0)
        {
            TempData["PidError"] = "No equipment found for this run.";
            return RedirectToAction(nameof(Index), new { projectId, runId = simulationRunId });
        }

        _layout.Apply(diagram);

        var id = await _pid.CreateAsync(diagram, User?.Identity?.Name, cancellationToken);
        return RedirectToAction(nameof(Editor), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Editor(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var model = new PidEditorViewModel
        {
            ProjectId = d.ProjectId,
            ProjectName = await _pid.GetProjectNameAsync(d.ProjectId, cancellationToken) ?? string.Empty,
            SimulationRunId = d.SimulationRunId,
            PidDiagramId = d.Id,
            Diagram = d,
            AvailableRuns = await _pid.GetRunOptionsAsync(d.ProjectId, cancellationToken),
            Revisions = await _pid.GetRevisionsAsync(id, cancellationToken),
            IsReadOnly = d.Status == PidApprovalStatus.Approved
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Data(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();
        return Json(d, PidJson);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        var dto = await ReadBodyAsync<PidDiagramDto>(cancellationToken);
        if (dto == null || dto.Id == Guid.Empty) return BadRequest("Invalid diagram.");

        try
        {
            var saved = await _pid.SaveAsync(dto, User?.Identity?.Name, cancellationToken);
            return saved == null ? NotFound() : Json(saved, PidJson);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(CancellationToken cancellationToken)
    {
        var req = await ReadBodyAsync<PidStatusRequest>(cancellationToken);
        if (req == null || req.Id == Guid.Empty) return BadRequest("Invalid request.");

        var ok = await _pid.ChangeStatusAsync(req.Id, req.Status, User?.Identity?.Name, req.Remarks, cancellationToken);
        if (!ok) return NotFound();

        return Json(new { ok = true, status = req.Status.ToString() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRevision(CancellationToken cancellationToken)
    {
        var req = await ReadBodyAsync<PidRevisionRequest>(cancellationToken);
        if (req == null || req.Id == Guid.Empty) return BadRequest("Invalid request.");

        var ok = await _pid.CreateRevisionAsync(req.Id, User?.Identity?.Name, req.Remarks, cancellationToken);
        if (!ok) return NotFound();

        return Json(new { ok = true });
    }

    [HttpGet]
    public async Task<IActionResult> Revisions(Guid id, CancellationToken cancellationToken)
    {
        var list = await _pid.GetRevisionsAsync(id, cancellationToken);
        return Json(list, PidJson);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var bytes = await _export.BuildListsExcelAsync(id, cancellationToken);
        var fileName = $"{d.DrawingNo}_Rev{d.RevisionNo}_Lists.xlsx";
        return File(bytes, XlsxMime, fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        await _pid.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { projectId = d.ProjectId });
    }
}
