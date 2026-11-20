using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize(Roles = AdminController.PlatformRole + "," + AdminController.ClientRole)]
public class EnquiryInviteController : Controller
{
    private readonly IEnquiryInviteRepository _invites;
    private readonly ILogger<EnquiryInviteController> _logger;

    public EnquiryInviteController(
        IEnquiryInviteRepository invites,
        ILogger<EnquiryInviteController> logger)
    {
        _invites = invites;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildIndexAsync(new EnquiryInviteFormViewModel(), cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnquiryInviteFormViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.Phone) && string.IsNullOrWhiteSpace(form.Email))
        {
            ModelState.AddModelError(string.Empty, "Enter a phone number or an email address.");
        }

        if (!ModelState.IsValid)
        {
            var invalid = await BuildIndexAsync(form, cancellationToken);
            return View(nameof(Index), invalid);
        }

        try
        {
            var invite = await _invites.CreateAsync(form, cancellationToken);
            TempData["Success"] = $"Invite created for {invite.CustomerName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invite creation failed");
            TempData["Error"] = "Invite could not be created. Try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _invites.CancelAsync(id, cancellationToken);
            TempData["Success"] = "Invite cancelled.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<EnquiryInviteIndexViewModel> BuildIndexAsync(
        EnquiryInviteFormViewModel form,
        CancellationToken cancellationToken)
    {
        var invites = await _invites.GetAllAsync(cancellationToken);

        foreach (var invite in invites)
        {
            invite.Link = Url.Action("Create", "PublicEnquiry", new { token = invite.Token }, Request.Scheme) ?? string.Empty;
        }

        return new EnquiryInviteIndexViewModel
        {
            Form = form,
            Invites = invites,
            GenericLink = Url.Action("Create", "PublicEnquiry", null, Request.Scheme) ?? string.Empty
        };
    }
}
