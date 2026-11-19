using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class FeedstockRepository : IFeedstockRepository
{
    private readonly ApplicationDbContext _db;

    public FeedstockRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<Feedstock>> GetAllAsync(
        string? search = null,
        FeedstockCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Feedstocks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f => f.Name.Contains(term));
        }

        if (category.HasValue)
            query = query.Where(f => f.Category == category.Value);

        return query
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Feedstock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Feedstocks
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<List<Feedstock>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();

        if (idList.Count == 0)
            return Task.FromResult(new List<Feedstock>());

        return _db.Feedstocks
            .AsNoTracking()
            .Where(f => idList.Contains(f.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? string.Empty).Trim();

        return _db.Feedstocks.AnyAsync(
            f => f.Name == trimmed && (!excludeId.HasValue || f.Id != excludeId.Value),
            cancellationToken);
    }

    /// <exception cref="InvalidOperationException">A feedstock with this name already exists.</exception>
    public async Task<Feedstock> AddAsync(Feedstock feedstock, CancellationToken cancellationToken = default)
    {
        feedstock.Name = feedstock.Name.Trim();

        if (await NameExistsAsync(feedstock.Name, null, cancellationToken))
            throw new InvalidOperationException($"A feedstock named '{feedstock.Name}' already exists.");

        if (feedstock.Id == Guid.Empty)
            feedstock.Id = Guid.NewGuid();

        feedstock.CreatedAt = DateTime.UtcNow;

        _db.Feedstocks.Add(feedstock);
        await _db.SaveChangesAsync(cancellationToken);

        return feedstock;
    }

    /// <summary>Copies every editable field; Id and CreatedAt are never changed.</summary>
    /// <exception cref="KeyNotFoundException">Feedstock does not exist.</exception>
    /// <exception cref="InvalidOperationException">Another feedstock already uses this name.</exception>
    public async Task UpdateAsync(Feedstock feedstock, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Feedstocks.FirstOrDefaultAsync(f => f.Id == feedstock.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feedstock {feedstock.Id} was not found.");

        var name = feedstock.Name.Trim();

        if (await NameExistsAsync(name, feedstock.Id, cancellationToken))
            throw new InvalidOperationException($"A feedstock named '{name}' already exists.");

        entity.Name = name;
        entity.Category = feedstock.Category;
        entity.TsPercent = feedstock.TsPercent;
        entity.VsPercentOfTs = feedstock.VsPercentOfTs;
        entity.CnRatio = feedstock.CnRatio;
        entity.BmpNm3PerKgVs = feedstock.BmpNm3PerKgVs;
        entity.BulkDensityKgM3 = feedstock.BulkDensityKgM3;
        entity.DegradationFactor = feedstock.DegradationFactor;
        entity.DefaultCostPerTon = feedstock.DefaultCostPerTon;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Feedstock does not exist.</exception>
    /// <exception cref="InvalidOperationException">Feedstock is used by one or more projects.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Feedstocks.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feedstock {id} was not found.");

        if (await _db.ProjectFeedstockMixes.AnyAsync(m => m.FeedstockId == id, cancellationToken))
            throw new InvalidOperationException(
                $"Feedstock '{entity.Name}' is used by one or more projects and cannot be deleted.");

        _db.Feedstocks.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}