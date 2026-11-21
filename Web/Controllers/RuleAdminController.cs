using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class RuleAdminViewModel
{
    public ClearanceRuleSet? RuleSet { get; set; }
    public List<ClearanceRule> Rules { get; set; } = new();
    public List<ClearanceSetting> Settings { get; set; } = new();
}

public class RuleEditModel
{
    public string Kind { get; set; } = "Rule";   // Rule or Setting
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    // Rule only
    public string FromHazardClass { get; set; } = string.Empty;
    public string ToHazardClass { get; set; } = string.Empty;
    public decimal? CapacityMinL { get; set; }
    public decimal? CapacityMaxL { get; set; }
    public string? SourceUrl { get; set; }

    // Setting only
    public string SettingKey { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    // Rule: minimum distance in metres. Setting: value in the given unit.
    public decimal Value { get; set; }

    public string Status { get; set; } = nameof(RuleStatus.Assumed);
    public string SourceNote { get; set; } = string.Empty;

    public bool EngineerConfirmed { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public static readonly string[] Statuses = { "Official", "Reference", "Assumed" };
}

[Authorize(Roles = AdminController.PlatformRole)]
[Route("Rules/[action]")]
public class RuleAdminController : Controller
{
    private const string IndexView = "~/Views/Rules/Index.cshtml";
    private const string EditView = "~/Views/Rules/Edit.cshtml";

    private readonly IClearanceRuleRepository _rules;

    public RuleAdminController(IClearanceRuleRepository rules)
    {
        _rules = rules;
    }

    [HttpGet("/Rules")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var set = await _rules.GetActiveRuleSetAsync(cancellationToken);
        var model = new RuleAdminViewModel { RuleSet = set };

        if (set != null)
        {
            model.Rules = await _rules.GetRulesAsync(set.Id, cancellationToken);
            model.Settings = await _rules.GetSettingsAsync(set.Id, cancellationToken);
        }

        return View(IndexView, model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string kind, Guid id, CancellationToken cancellationToken)
    {
        if (kind == "Setting")
        {
            var s = await _rules.GetSettingAsync(id, cancellationToken);
            if (s == null) return NotFound();

            return View(EditView, new RuleEditModel
            {
                Kind = "Setting",
                Id = s.Id,
                Title = s.SettingKey,
                SettingKey = s.SettingKey,
                Unit = s.Unit,
                Value = s.ValueNum,
                Status = s.Status.ToString(),
                SourceNote = s.SourceNote,
                EngineerConfirmed = s.EngineerConfirmed,
                ConfirmedBy = s.ConfirmedBy,
                ConfirmedAt = s.ConfirmedAt
            });
        }

        var r = await _rules.GetRuleAsync(id, cancellationToken);
        if (r == null) return NotFound();

        return View(EditView, new RuleEditModel
        {
            Kind = "Rule",
            Id = r.Id,
            Title = $"{r.FromHazardClass} to {r.ToHazardClass}",
            FromHazardClass = r.FromHazardClass.ToString(),
            ToHazardClass = r.ToHazardClass.ToString(),
            CapacityMinL = r.CapacityMinL,
            CapacityMaxL = r.CapacityMaxL,
            Value = r.MinDistanceM,
            Status = r.Status.ToString(),
            SourceNote = r.SourceNote,
            SourceUrl = r.SourceUrl,
            EngineerConfirmed = r.EngineerConfirmed,
            ConfirmedBy = r.ConfirmedBy,
            ConfirmedAt = r.ConfirmedAt
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RuleEditModel model, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RuleStatus>(model.Status, true, out var status))
            ModelState.AddModelError(nameof(model.Status), "Choose Official, Reference or Assumed.");

        if (model.Value < 0)
            ModelState.AddModelError(nameof(model.Value), "Value cannot be negative.");

        if (string.IsNullOrWhiteSpace(model.SourceNote))
            ModelState.AddModelError(nameof(model.SourceNote), "Source note is required.");

        if (model.CapacityMinL.HasValue && model.CapacityMaxL.HasValue && model.CapacityMaxL < model.CapacityMinL)
            ModelState.AddModelError(nameof(model.CapacityMaxL), "Maximum capacity must be at least the minimum.");

        if (!ModelState.IsValid) return View(EditView, model);

        if (model.Kind == "Setting")
        {
            await _rules.UpdateSettingAsync(new ClearanceSetting
            {
                Id = model.Id,
                ValueNum = model.Value,
                Unit = model.Unit,
                Status = status,
                SourceNote = model.SourceNote.Trim()
            }, cancellationToken);
        }
        else
        {
            await _rules.UpdateRuleAsync(new ClearanceRule
            {
                Id = model.Id,
                MinDistanceM = model.Value,
                CapacityMinL = model.CapacityMinL,
                CapacityMaxL = model.CapacityMaxL,
                Status = status,
                SourceNote = model.SourceNote.Trim(),
                SourceUrl = string.IsNullOrWhiteSpace(model.SourceUrl) ? null : model.SourceUrl.Trim()
            }, cancellationToken);
        }

        TempData["Success"] = "Saved. Engineer confirmation was cleared; confirm again after review.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string kind, Guid id, CancellationToken cancellationToken)
    {
        string by = User.Identity?.Name ?? "unknown";

        if (kind == "Setting")
            await _rules.ConfirmSettingAsync(id, by, cancellationToken);
        else
            await _rules.ConfirmRuleAsync(id, by, cancellationToken);

        TempData["Success"] = "Marked as confirmed by engineer.";
        return RedirectToAction(nameof(Index));
    }
}