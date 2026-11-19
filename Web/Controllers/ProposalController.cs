using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class ProposalController : Controller
{
    private readonly IProposalExport _proposals;
    private readonly ISimulationRepository _simulations;
    private readonly IProjectRepository _projects;
    private readonly ILogger<ProposalController> _logger;

    public ProposalController(
        IProposalExport proposals,
        ISimulationRepository simulations,
        IProjectRepository projects,
        ILogger<ProposalController> logger)
    {
        _proposals = proposals;
        _simulations = simulations;
        _projects = projects;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid runId, CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(runId, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Financials(Guid runId, CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(runId, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Financials(ProposalViewModel model, CancellationToken cancellationToken)
    {
        var shell = await BuildModelAsync(model.SimulationRunId, cancellationToken);
        if (shell == null)
        {
            return NotFound();
        }

        model.ProjectId = shell.ProjectId;
        model.ProjectName = shell.ProjectName;
        model.FinancialInputs ??= new FinancialInputsDto();
        model.RateCardOverrides = (model.RateCardOverrides ?? new List<RateCardOverrideDto>())
            .Where(r => !string.IsNullOrWhiteSpace(r.ItemCode))
            .ToList();

        Validate(model.FinancialInputs);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var response = await _proposals.ExportProposalAsync(new ProposalRequestDto
            {
                SimulationRunId = model.SimulationRunId,
                FinancialInputs = model.FinancialInputs,
                CustomRateCardOverrides = model.RateCardOverrides
            }, cancellationToken);

            model.ProposalId = response.ProposalId;
            model.Metrics = response.FinancialMetrics;
            model.ExcelBomUrl = response.ExcelBomUrl;
            model.ProposalPdfUrl = response.ProposalPdfUrl;

            ModelState.Clear();
            TempData["Success"] = "Proposal generated.";
            return View(model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proposal export failed for run {RunId}", model.SimulationRunId);
            ModelState.AddModelError(string.Empty, "Proposal generation failed. Check the inputs and try again.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Bom(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }

        var project = await _projects.GetByIdAsync(run.ProjectId, cancellationToken);
        var equipment = await _simulations.GetEquipmentAsync(runId, cancellationToken);

        ViewData["SimulationRunId"] = runId;
        ViewData["ProjectId"] = run.ProjectId;
        ViewData["ProjectName"] = project?.Name ?? string.Empty;
        return View(equipment);
    }

    [HttpGet]
    public async Task<IActionResult> BomExcel(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }

        try
        {
            var bytes = await _proposals.BuildBomExcelAsync(runId, null, cancellationToken);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BOM_{runId.ToString("N")[..8]}_v{run.RunVersion}.xlsx");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ProposalViewModel?> BuildModelAsync(Guid runId, CancellationToken cancellationToken)
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

        return new ProposalViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            SimulationRunId = runId
        };
    }

    private void Validate(FinancialInputsDto inputs)
    {
        if (inputs.CbgSellingPricePerKg < 0)
        {
            ModelState.AddModelError("FinancialInputs.CbgSellingPricePerKg", "CBG price cannot be negative.");
        }

        if (inputs.FomSellingPricePerTon < 0)
        {
            ModelState.AddModelError("FinancialInputs.FomSellingPricePerTon", "FOM price cannot be negative.");
        }

        if (inputs.DiscountRatePercent is < 0 or > 100)
        {
            ModelState.AddModelError("FinancialInputs.DiscountRatePercent", "Discount rate must be between 0 and 100.");
        }

        if (inputs.ProjectLifeYears is < 1 or > 50)
        {
            ModelState.AddModelError("FinancialInputs.ProjectLifeYears", "Project life must be between 1 and 50 years.");
        }

        if (inputs.DebtRatioPercent is < 0 or > 100)
        {
            ModelState.AddModelError("FinancialInputs.DebtRatioPercent", "Debt ratio must be between 0 and 100.");
        }

        if (inputs.InterestRatePercent is < 0 or > 100)
        {
            ModelState.AddModelError("FinancialInputs.InterestRatePercent", "Interest rate must be between 0 and 100.");
        }
    }
}