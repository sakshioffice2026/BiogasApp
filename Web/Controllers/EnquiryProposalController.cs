using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize(Roles = AdminController.PlatformRole + "," + AdminController.ClientRole)]
public class EnquiryProposalController : Controller
{
    private readonly IEnquiryProposalRepository _proposals;
    private readonly ILogger<EnquiryProposalController> _logger;

    public EnquiryProposalController(
        IEnquiryProposalRepository proposals,
        ILogger<EnquiryProposalController> logger)
    {
        _proposals = proposals;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid enquiryId, CancellationToken cancellationToken)
    {
        var model = await _proposals.GetFormAsync(enquiryId, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        if (model.AssessmentId == null)
        {
            TempData["Error"] = "Create a site assessment for this enquiry first.";
            return RedirectToAction("Details", "Enquiry", new { id = enquiryId });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnquiryProposalFormViewModel model, CancellationToken cancellationToken)
    {
        model.FeedstockLines = (model.FeedstockLines ?? new List<ProposalFeedstockLineViewModel>())
            .Where(l => l.FeedstockId != Guid.Empty && l.DailyTonnageTpd > 0)
            .ToList();
        model.FinancialInputs ??= new FinancialInputsDto();

        if (model.FeedstockLines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one feedstock with a quantity above zero.");
        }

        ValidateFinancials(model.FinancialInputs);

        if (!ModelState.IsValid)
        {
            await RefillAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var proposal = await _proposals.CreateAsync(model, cancellationToken);
            TempData["Success"] = $"Proposal {proposal.ProposalNo} generated.";
            return RedirectToAction(nameof(Details), new { id = proposal.Id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proposal creation failed for enquiry {EnquiryId}", model.EnquiryId);
            ModelState.AddModelError(string.Empty, "Proposal generation failed. Check the inputs and try again.");
        }

        await RefillAsync(model, CancellationToken.None);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _proposals.GetDetailsAsync(id, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var proposal = await _proposals.GetByIdAsync(id, cancellationToken);
        if (proposal == null)
        {
            return NotFound();
        }

        var bytes = await _proposals.GetPdfAsync(id, cancellationToken);
        if (bytes == null)
        {
            return NotFound();
        }

        return File(bytes, "application/pdf", $"{proposal.ProposalNo}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(EnquiryProposalStatusUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid status update.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        try
        {
            await _proposals.SetStatusAsync(model.Id, model.Status, model.Remarks, cancellationToken);
            TempData["Success"] = "Proposal status updated.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private async Task RefillAsync(EnquiryProposalFormViewModel model, CancellationToken cancellationToken)
    {
        var fresh = await _proposals.GetFormAsync(model.EnquiryId, cancellationToken);
        if (fresh == null)
        {
            return;
        }

        model.EnquiryNo = fresh.EnquiryNo;
        model.CustomerName = fresh.CustomerName;
        model.FeedstockRequested = fresh.FeedstockRequested;
        model.QuantityRequestedTpd = fresh.QuantityRequestedTpd;
        model.EndUseRequested = fresh.EndUseRequested;
        model.NextVersion = fresh.NextVersion;
        model.ProjectId = fresh.ProjectId;
        model.AssessmentId = fresh.AssessmentId;
        model.AvailableFeedstocks = fresh.AvailableFeedstocks;

        if (model.FeedstockLines.Count == 0)
        {
            model.FeedstockLines.Add(new ProposalFeedstockLineViewModel());
        }
    }

    private void ValidateFinancials(FinancialInputsDto inputs)
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