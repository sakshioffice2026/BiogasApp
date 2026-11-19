using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class FeedstockController : Controller
{
    private readonly IFeedstockRepository _feedstocks;
    private readonly ILogger<FeedstockController> _logger;

    public FeedstockController(IFeedstockRepository feedstocks, ILogger<FeedstockController> logger)
    {
        _feedstocks = feedstocks;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        FeedstockCategory? category,
        CancellationToken cancellationToken)
    {
        var items = await _feedstocks.GetAllAsync(search, category, cancellationToken);

        var model = new FeedstockIndexViewModel
        {
            Search = search,
            CategoryFilter = category,
            Feedstocks = items.Select(ToViewModel).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new FeedstockViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FeedstockViewModel model, CancellationToken cancellationToken)
    {
        if (await _feedstocks.NameExistsAsync(model.Name.Trim(), null, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Name), "A feedstock with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Feedstock
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        Apply(entity, model);

        await _feedstocks.AddAsync(entity, cancellationToken);
        TempData["Success"] = "Feedstock created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _feedstocks.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        return View(ToViewModel(entity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, FeedstockViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        if (await _feedstocks.NameExistsAsync(model.Name.Trim(), id, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Name), "A feedstock with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _feedstocks.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        Apply(entity, model);

        await _feedstocks.UpdateAsync(entity, cancellationToken);
        TempData["Success"] = "Feedstock updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _feedstocks.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Feedstock deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete feedstock {FeedstockId}", id);
            TempData["Error"] = "Feedstock could not be deleted. It may be used in a project mix.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static FeedstockViewModel ToViewModel(Feedstock f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Category = f.Category,
        TsPercent = f.TsPercent,
        VsPercentOfTs = f.VsPercentOfTs,
        CnRatio = f.CnRatio,
        BmpNm3PerKgVs = f.BmpNm3PerKgVs,
        BulkDensityKgM3 = f.BulkDensityKgM3,
        DegradationFactor = f.DegradationFactor,
        DefaultCostPerTon = f.DefaultCostPerTon
    };

    private static void Apply(Feedstock entity, FeedstockViewModel model)
    {
        entity.Name = model.Name.Trim();
        entity.Category = model.Category;
        entity.TsPercent = model.TsPercent;
        entity.VsPercentOfTs = model.VsPercentOfTs;
        entity.CnRatio = model.CnRatio;
        entity.BmpNm3PerKgVs = model.BmpNm3PerKgVs;
        entity.BulkDensityKgM3 = model.BulkDensityKgM3;
        entity.DegradationFactor = model.DegradationFactor;
        entity.DefaultCostPerTon = model.DefaultCostPerTon;
    }
}