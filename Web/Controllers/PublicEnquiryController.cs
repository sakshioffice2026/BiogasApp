using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[AllowAnonymous]
public class PublicEnquiryController : Controller
{
    private readonly IEnquiryRepository _enquiries;
    private readonly IEnquiryInviteRepository _invites;
    private readonly ILogger<PublicEnquiryController> _logger;

    public PublicEnquiryController(
        IEnquiryRepository enquiries,
        IEnquiryInviteRepository invites,
        ILogger<PublicEnquiryController> logger)
    {
        _enquiries = enquiries;
        _invites = invites;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? token, CancellationToken cancellationToken)
    {
        var model = new EnquiryFormViewModel { Source = EnquirySource.Website };

        if (!string.IsNullOrWhiteSpace(token))
        {
            var invite = await _invites.ValidateTokenAsync(token, cancellationToken);
            if (invite.IsValid)
            {
                model.Token = token;
                model.CustomerName = invite.CustomerName ?? string.Empty;
                model.Phone = invite.Phone ?? string.Empty;
                model.Email = invite.Email ?? string.Empty;
            }
            else
            {
                ModelState.AddModelError(string.Empty, "This link has expired or was already used. You can still fill the form below.");
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnquiryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model.Token))
        {
            var invite = await _invites.ValidateTokenAsync(model.Token, cancellationToken);
            if (!invite.IsValid)
            {
                ModelState.Remove(nameof(model.Token));
                model.Token = null;
                ModelState.AddModelError(string.Empty, "This link has expired or was already used.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var enquiry = new Enquiry
        {
            Id = Guid.NewGuid(),
            CustomerName = model.CustomerName,
            CompanyName = model.CompanyName,
            Email = model.Email,
            Phone = model.Phone,
            LocationCity = model.LocationCity,
            LocationState = model.LocationState,
            LocationCountry = model.LocationCountry,
            FeedstockType = model.FeedstockType,
            FeedstockQtyTpd = model.FeedstockQtyTpd,
            EndUse = model.EndUse,
            Message = model.Message,
            Source = EnquirySource.Website
        };

        var created = await _enquiries.AddAsync(enquiry, cancellationToken);
        _logger.LogInformation("Public enquiry {EnquiryNo} received.", created.EnquiryNo);

        if (!string.IsNullOrWhiteSpace(model.Token))
        {
            await _invites.MarkSubmittedAsync(model.Token, created.Id, cancellationToken);
        }

        TempData["EnquiryNo"] = created.EnquiryNo;
        return RedirectToAction(nameof(ThankYou));
    }

    [HttpGet]
    public IActionResult ThankYou()
    {
        ViewData["EnquiryNo"] = TempData["EnquiryNo"];
        return View();
    }
}