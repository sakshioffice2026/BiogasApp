using BiogasApp.Business.Pid;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private readonly IProjectRepository _projects;
    private readonly IPidRepository _pid;
    private readonly IPidExport _export;
    private readonly ISiteRepository _sites;
    private readonly PidTemplateBuilder _template;
    private readonly PidAutoLayoutService _layout;
    private readonly IDatasheetExport _datasheet;

    public PidController(
    IPidRepository pid,
    IPidExport export,
    IDatasheetExport datasheet,
    IProjectRepository projects,
    ISiteRepository sites,
    PidTemplateBuilder template,
    PidAutoLayoutService layout)
    {
        _pid = pid;
        _export = export;
        _datasheet = datasheet;
        _projects = projects;
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



    [HttpGet]
    public async Task<IActionResult> ExportDxf(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var projectName = await _pid.GetProjectNameAsync(d.ProjectId, cancellationToken) ?? string.Empty;
        var bytes = PidDxfWriter.Write(d, projectName, DateTime.UtcNow);
        var fileName = $"{d.DrawingNo}_Rev{d.RevisionNo}.dxf";
        return File(bytes, "application/dxf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var projectName = await _pid.GetProjectNameAsync(d.ProjectId, cancellationToken) ?? string.Empty;
        var bytes = PidPdfWriter.Write(d, projectName, DateTime.UtcNow);
        var fileName = $"{d.DrawingNo}_Rev{d.RevisionNo}.pdf";
        return File(bytes, "application/pdf", fileName);
    }


    [HttpGet]
    public async Task<IActionResult> ExportDatasheetExcel(Guid id, string? tag, string? view, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var v = (view ?? string.Empty).Trim().ToLowerInvariant();
        if (v != "client" && v != "vendor") v = "epc";

        try
        {
            var bytes = await _datasheet.BuildDatasheetExcelAsync(id, tag, v, cancellationToken);
            var suffix = string.IsNullOrWhiteSpace(tag) ? "Datasheets" : $"Datasheet_{tag.Trim()}";
            var viewSuffix = v == "client" ? "Client" : v == "vendor" ? "VendorRFQ" : "EPC";
            var fileName = $"{d.DrawingNo}_Rev{d.RevisionNo}_{suffix}_{viewSuffix}.xlsx";
            return File(bytes, XlsxMime, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }


    [HttpGet]
    public async Task<IActionResult> ExportDatasheetPdf(Guid id, string? tag, string? view, CancellationToken cancellationToken)
    {
        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        var v = (view ?? string.Empty).Trim().ToLowerInvariant();
        if (v != "client" && v != "vendor") v = "epc";

        try
        {
            var bytes = await _datasheet.BuildDatasheetPdfAsync(id, tag, v, cancellationToken);
            var suffix = string.IsNullOrWhiteSpace(tag) ? "Datasheets" : $"Datasheet_{tag.Trim()}";
            var viewSuffix = v == "client" ? "Client" : v == "vendor" ? "VendorRFQ" : "EPC";
            var fileName = $"{d.DrawingNo}_Rev{d.RevisionNo}_{suffix}_{viewSuffix}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Datasheets(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var set = await _datasheet.GetDatasheetsAsync(id, cancellationToken);
            return Json(set, PidJson);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVendorData(Guid id, CancellationToken cancellationToken)
    {
        var dto = await ReadBodyAsync<EquipmentVendorDataDto>(cancellationToken);
        if (dto == null || string.IsNullOrWhiteSpace(dto.Tag)) return BadRequest("Invalid vendor data.");

        var d = await _pid.GetByIdAsync(id, cancellationToken);
        if (d == null) return NotFound();

        await _datasheet.SaveVendorDataAsync(id, dto, User?.Identity?.Name, cancellationToken);
        return Json(new { ok = true });
    }

    [HttpGet]
    public async Task<IActionResult> DatasheetProjects(CancellationToken cancellationToken)
    {
        var model = await _projects.GetIndexAsync(null, null, cancellationToken);

        var drawings = new Dictionary<Guid, List<PidDiagramDto>>();
        foreach (var p in model.Projects)
            drawings[p.Id] = await _pid.GetByProjectAsync(p.Id, cancellationToken);

        ViewData["Drawings"] = drawings;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DatasheetPage(Guid id, string? tag, string? view, CancellationToken cancellationToken)
    {
        try
        {
            var set = await _datasheet.GetDatasheetsAsync(id, cancellationToken);
            var v = (view ?? string.Empty).Trim().ToLowerInvariant();
            if (v != "client" && v != "vendor") v = "epc";
            ViewData["SelectedTag"] = tag;
            ViewData["View"] = v;
            return View(set);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
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
