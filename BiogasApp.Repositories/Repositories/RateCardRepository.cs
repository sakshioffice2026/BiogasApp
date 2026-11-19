using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class RateCardRepository : IRateCardRepository
{
    // Allowed values follow the original spec's CHECK constraints. Edit here if your table differs.
    private static readonly string[] AllowedCategories = { "Civil", "Structural", "Mechanical", "Piping", "Electrical" };
    private static readonly string[] AllowedUnits = { "m3", "kg", "m", "nos", "m2", "ls" };

    private const decimal MaxUnitRate = 99_999_999.99m; // DECIMAL(10,2)

    private readonly ApplicationDbContext _db;

    public RateCardRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<VendorRateCard>> GetAllAsync(
        Guid tenantId,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.VendorRateCards
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(r => r.Category == cat);
        }

        return query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.ItemCode)
            .ToListAsync(cancellationToken);
    }

    public Task<VendorRateCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.VendorRateCards
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<VendorRateCard?> GetByItemCodeAsync(
        Guid tenantId,
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        var code = (itemCode ?? string.Empty).Trim();

        return _db.VendorRateCards
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ItemCode == code, cancellationToken);
    }

    /// <summary>ItemCode -> UnitRate (case-insensitive). If a code is duplicated, the most recently updated wins.</summary>
    public async Task<Dictionary<string, decimal>> GetRateMapAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.VendorRateCards
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.UpdatedAt)
            .Select(r => new { r.ItemCode, r.UnitRate })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            map[row.ItemCode] = row.UnitRate;

        return map;
    }

    /// <exception cref="ArgumentException">A field is missing or out of range.</exception>
    /// <exception cref="InvalidOperationException">The item code already exists for this tenant.</exception>
    public async Task<VendorRateCard> AddAsync(VendorRateCard rateCard, CancellationToken cancellationToken = default)
    {
        Normalize(rateCard);

        if (await _db.VendorRateCards.AnyAsync(
                r => r.TenantId == rateCard.TenantId && r.ItemCode == rateCard.ItemCode, cancellationToken))
            throw new InvalidOperationException($"Item code '{rateCard.ItemCode}' already exists.");

        if (rateCard.Id == Guid.Empty)
            rateCard.Id = Guid.NewGuid();

        rateCard.UpdatedAt = DateTime.UtcNow;

        _db.VendorRateCards.Add(rateCard);
        await _db.SaveChangesAsync(cancellationToken);

        return rateCard;
    }

    /// <summary>Copies every editable field; Id and TenantId are never changed.</summary>
    /// <exception cref="KeyNotFoundException">Rate card does not exist.</exception>
    /// <exception cref="InvalidOperationException">Another rate card of this tenant uses the item code.</exception>
    public async Task UpdateAsync(VendorRateCard rateCard, CancellationToken cancellationToken = default)
    {
        var entity = await _db.VendorRateCards.FirstOrDefaultAsync(r => r.Id == rateCard.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Rate card {rateCard.Id} was not found.");

        Normalize(rateCard);

        if (await _db.VendorRateCards.AnyAsync(
                r => r.TenantId == entity.TenantId && r.ItemCode == rateCard.ItemCode && r.Id != entity.Id,
                cancellationToken))
            throw new InvalidOperationException($"Item code '{rateCard.ItemCode}' already exists.");

        entity.ItemCode = rateCard.ItemCode;
        entity.Category = rateCard.Category;
        entity.Description = rateCard.Description;
        entity.Unit = rateCard.Unit;
        entity.UnitRate = rateCard.UnitRate;
        entity.Currency = rateCard.Currency;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Rate card does not exist.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.VendorRateCards.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Rate card {id} was not found.");

        _db.VendorRateCards.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Trims, canonicalises casing for category/unit/currency, and validates against column limits.</summary>
    private static void Normalize(VendorRateCard card)
    {
        card.ItemCode = (card.ItemCode ?? string.Empty).Trim();
        if (card.ItemCode.Length == 0 || card.ItemCode.Length > 50)
            throw new ArgumentException("Item code is required (max 50 characters).", nameof(card));

        var category = AllowedCategories.FirstOrDefault(
            c => c.Equals((card.Category ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        card.Category = category
            ?? throw new ArgumentException($"Category must be one of: {string.Join(", ", AllowedCategories)}.", nameof(card));

        var unit = AllowedUnits.FirstOrDefault(
            u => u.Equals((card.Unit ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        card.Unit = unit
            ?? throw new ArgumentException($"Unit must be one of: {string.Join(", ", AllowedUnits)}.", nameof(card));

        card.Description = (card.Description ?? string.Empty).Trim();
        if (card.Description.Length == 0)
            throw new ArgumentException("Description is required.", nameof(card));

        var currency = (card.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new ArgumentException("Currency must be a 3-letter code (for example INR).", nameof(card));
        card.Currency = currency;

        if (card.UnitRate < 0 || card.UnitRate > MaxUnitRate)
            throw new ArgumentException($"Unit rate must be between 0 and {MaxUnitRate:N2}.", nameof(card));
    }
}