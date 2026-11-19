using System.Security.Claims;
using BiogasApp.Database.Entities;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize(Roles = PlatformRole + "," + ClientRole)]
public class AdminController : Controller
{
    public const string PlatformRole = "SuperAdmin";
    public const string ClientRole = "ClientAdmin";

    private static readonly string[] RateCategories = { "Civil", "Structural", "Mechanical", "Piping", "Electrical" };
    private static readonly string[] RateUnits = { "m3", "kg", "m", "nos", "m2", "ls" };

    private readonly IAdminRepository _admin;
    private readonly ITenant _tenants;
    private readonly IRateCardRepository _rateCards;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminRepository admin,
        ITenant tenants,
        IRateCardRepository rateCards,
        ILogger<AdminController> logger)
    {
        _admin = admin;
        _tenants = tenants;
        _rateCards = rateCards;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Overview

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _admin.GetOverviewAsync(ScopeTenantId(), cancellationToken);
        return View(model);
    }

    // ------------------------------------------------------------------ Users

    [HttpGet]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var users = await _admin.GetUsersAsync(ScopeTenantId(), cancellationToken);
        ViewData["Roles"] = await _admin.GetRolesAsync(cancellationToken);
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (id == CurrentUserId())
        {
            TempData["Error"] = "You cannot change your own active status.";
            return RedirectToAction(nameof(Users));
        }

        if (!await UserInScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        await _admin.SetUserActiveAsync(id, isActive, cancellationToken);
        TempData["Success"] = isActive ? "User activated." : "User deactivated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserRole(Guid id, int roleId, CancellationToken cancellationToken)
    {
        if (id == CurrentUserId())
        {
            TempData["Error"] = "You cannot change your own role.";
            return RedirectToAction(nameof(Users));
        }

        if (!await UserInScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var roles = await _admin.GetRolesAsync(cancellationToken);
        var target = roles.FirstOrDefault(r => r.Id == roleId);
        if (target == null)
        {
            return BadRequest();
        }

        if (!User.IsInRole(PlatformRole) &&
            string.Equals(target.Name, PlatformRole, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        await _admin.ChangeUserRoleAsync(id, roleId, cancellationToken);
        TempData["Success"] = "User role updated.";
        return RedirectToAction(nameof(Users));
    }

    // ------------------------------------------------------------------ Rate cards

    [HttpGet]
    public async Task<IActionResult> RateCards(string? category, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId == null)
        {
            return Forbid();
        }

        var cards = await _rateCards.GetAllAsync(tenantId.Value, category, cancellationToken);
        ViewData["Category"] = category;
        ViewData["Categories"] = RateCategories;
        ViewData["Units"] = RateUnits;
        return View(cards.Select(ToViewModel).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRateCard(RateCardViewModel model, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId == null)
        {
            return Forbid();
        }

        model.ItemCode = model.ItemCode?.Trim() ?? string.Empty;
        model.Currency = model.Currency?.Trim().ToUpperInvariant() ?? string.Empty;

        if (!RateCategories.Contains(model.Category))
        {
            ModelState.AddModelError(nameof(model.Category), "Invalid category.");
        }

        if (!RateUnits.Contains(model.Unit))
        {
            ModelState.AddModelError(nameof(model.Unit), "Invalid unit.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m)));
            return RedirectToAction(nameof(RateCards));
        }

        var existing = await _rateCards.GetByItemCodeAsync(tenantId.Value, model.ItemCode, cancellationToken);

        if (model.Id.HasValue)
        {
            var card = await _rateCards.GetByIdAsync(model.Id.Value, cancellationToken);
            if (card == null || card.TenantId != tenantId.Value)
            {
                return NotFound();
            }

            if (existing != null && existing.Id != card.Id)
            {
                TempData["Error"] = "Another rate card already uses this item code.";
                return RedirectToAction(nameof(RateCards));
            }

            card.ItemCode = model.ItemCode;
            card.Category = model.Category;
            card.Description = model.Description;
            card.Unit = model.Unit;
            card.UnitRate = model.UnitRate;
            card.Currency = model.Currency;
            card.UpdatedAt = DateTime.UtcNow;

            await _rateCards.UpdateAsync(card, cancellationToken);
            TempData["Success"] = "Rate card updated.";
        }
        else
        {
            if (existing != null)
            {
                TempData["Error"] = "A rate card with this item code already exists.";
                return RedirectToAction(nameof(RateCards));
            }

            await _rateCards.AddAsync(new VendorRateCard
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                ItemCode = model.ItemCode,
                Category = model.Category,
                Description = model.Description,
                Unit = model.Unit,
                UnitRate = model.UnitRate,
                Currency = model.Currency,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            TempData["Success"] = "Rate card added.";
        }

        return RedirectToAction(nameof(RateCards));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRateCard(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId == null)
        {
            return Forbid();
        }

        var card = await _rateCards.GetByIdAsync(id, cancellationToken);
        if (card == null || card.TenantId != tenantId.Value)
        {
            return NotFound();
        }

        await _rateCards.DeleteAsync(id, cancellationToken);
        TempData["Success"] = "Rate card deleted.";
        return RedirectToAction(nameof(RateCards));
    }

    // ------------------------------------------------------------------ Tenants

    [HttpGet]
    [Authorize(Roles = PlatformRole)]
    public async Task<IActionResult> Tenants(CancellationToken cancellationToken)
    {
        var tenants = await _tenants.GetAllAsync(cancellationToken);
        return View(tenants);
    }

    [HttpPost]
    [Authorize(Roles = PlatformRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(string name, CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 150)
        {
            TempData["Error"] = "Tenant name must be between 2 and 150 characters.";
            return RedirectToAction(nameof(Tenants));
        }

        if (await _tenants.GetByNameAsync(name, cancellationToken) != null)
        {
            TempData["Error"] = "A tenant with this name already exists.";
            return RedirectToAction(nameof(Tenants));
        }

        await _tenants.CreateAsync(name, cancellationToken);
        TempData["Success"] = "Tenant created.";
        return RedirectToAction(nameof(Tenants));
    }

    [HttpPost]
    [Authorize(Roles = PlatformRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTenantActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (id == CurrentTenantId() && !isActive)
        {
            TempData["Error"] = "You cannot deactivate your own tenant.";
            return RedirectToAction(nameof(Tenants));
        }

        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
        {
            return NotFound();
        }

        await _tenants.SetActiveAsync(id, isActive, cancellationToken);
        TempData["Success"] = isActive ? "Tenant activated." : "Tenant deactivated.";
        return RedirectToAction(nameof(Tenants));
    }

    // ------------------------------------------------------------------ Helpers

    private Guid? CurrentTenantId() =>
        Guid.TryParse(User.FindFirstValue(AccountController.TenantIdClaim), out var id) ? id : null;

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private Guid? ScopeTenantId() => User.IsInRole(PlatformRole) ? null : CurrentTenantId();

    private async Task<bool> UserInScopeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var users = await _admin.GetUsersAsync(ScopeTenantId(), cancellationToken);
        return users.Any(u => u.Id == userId);
    }

    private static RateCardViewModel ToViewModel(VendorRateCard c) => new()
    {
        Id = c.Id,
        ItemCode = c.ItemCode,
        Category = c.Category,
        Description = c.Description,
        Unit = c.Unit,
        UnitRate = c.UnitRate,
        Currency = c.Currency
    };
}