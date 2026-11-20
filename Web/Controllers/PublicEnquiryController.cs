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
    private readonly ILogger<PublicEnquiryController> _logger;

    public PublicEnquiryController(IEnquiryRepository enquiries, ILogger<PublicEnquiryController> logger)
    {
        _enquiries = enquiries;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EnquiryFormViewModel { Source = EnquirySource.Website });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnquiryFormViewModel model, CancellationToken cancellationToken)
    {
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
