using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize(Roles = AdminController.PlatformRole + "," + AdminController.ClientRole)]
public class EnquiryController : Controller
{
    private readonly IEnquiryRepository _enquiries;
    private readonly ILogger<EnquiryController> _logger;

    public EnquiryController(IEnquiryRepository enquiries, ILogger<EnquiryController> logger)
    {
        _enquiries = enquiries;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, EnquiryStatus? status, CancellationToken cancellationToken)
    {
        var model = await _enquiries.GetIndexAsync(search, status, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var enquiry = await _enquiries.GetByIdAsync(id, cancellationToken);
        if (enquiry == null)
        {
            return NotFound();
        }

        return View(ToDetails(enquiry));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EnquiryFormViewModel { Source = EnquirySource.Phone });
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
            Source = model.Source
        };

        var created = await _enquiries.AddAsync(enquiry, cancellationToken);
        TempData["Success"] = $"Enquiry {created.EnquiryNo} created.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var enquiry = await _enquiries.GetByIdAsync(id, cancellationToken);
        if (enquiry == null)
        {
            return NotFound();
        }

        var model = new EnquiryFormViewModel
        {
            Id = enquiry.Id,
            CustomerName = enquiry.CustomerName,
            CompanyName = enquiry.CompanyName,
            Email = enquiry.Email,
            Phone = enquiry.Phone,
            LocationCity = enquiry.LocationCity,
            LocationState = enquiry.LocationState,
            LocationCountry = enquiry.LocationCountry,
            FeedstockType = enquiry.FeedstockType,
            FeedstockQtyTpd = enquiry.FeedstockQtyTpd,
            EndUse = enquiry.EndUse,
            Message = enquiry.Message,
            Source = enquiry.Source
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EnquiryFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var enquiry = new Enquiry
        {
            Id = id,
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
            Source = model.Source
        };

        try
        {
            await _enquiries.UpdateAsync(enquiry, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["Success"] = "Enquiry updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(EnquiryStatusUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid status update.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        try
        {
            await _enquiries.SetStatusAsync(model.Id, model.Status, model.AdminNotes, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["Success"] = "Enquiry status updated.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private static EnquiryDetailsViewModel ToDetails(Enquiry e) => new()
    {
        Id = e.Id,
        EnquiryNo = e.EnquiryNo,
        CustomerName = e.CustomerName,
        CompanyName = e.CompanyName,
        Email = e.Email,
        Phone = e.Phone,
        LocationCity = e.LocationCity,
        LocationState = e.LocationState,
        LocationCountry = e.LocationCountry,
        FeedstockType = e.FeedstockType,
        FeedstockQtyTpd = e.FeedstockQtyTpd,
        EndUse = e.EndUse,
        Message = e.Message,
        Source = e.Source,
        Status = e.Status,
        AdminNotes = e.AdminNotes,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}
