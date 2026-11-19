using System.Linq.Expressions;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Model;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class TenantRepository : ITenant
{
    private const int MaxNameLength = 150; // matches tenants.Name HasMaxLength(150)

    private static readonly Expression<Func<Tenant, TenantModel>> ToModel = t => new TenantModel
    {
        Id = t.Id,
        Name = t.Name,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt
    };

    private readonly ApplicationDbContext _db;

    public TenantRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<TenantModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(ToModel)
            .ToListAsync(cancellationToken);

    public Task<TenantModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ToModel)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? string.Empty).Trim();

        return _db.Tenants
            .AsNoTracking()
            .Where(t => t.Name == trimmed)
            .Select(ToModel)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <exception cref="ArgumentException">Name is empty or longer than 150 characters.</exception>
    /// <exception cref="InvalidOperationException">A tenant with this name already exists.</exception>
    public async Task<TenantModel> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateName(name);

        if (await _db.Tenants.AnyAsync(t => t.Name == trimmed, cancellationToken))
            throw new InvalidOperationException($"A tenant named '{trimmed}' already exists.");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        return new TenantModel
        {
            Id = tenant.Id,
            Name = tenant.Name,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt
        };
    }

    /// <summary>Updates Name and IsActive only.</summary>
    /// <exception cref="KeyNotFoundException">Tenant does not exist.</exception>
    /// <exception cref="InvalidOperationException">Another tenant already uses this name.</exception>
    public async Task UpdateAsync(TenantModel tenant, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateName(tenant.Name);

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {tenant.Id} was not found.");

        if (await _db.Tenants.AnyAsync(t => t.Name == trimmed && t.Id != tenant.Id, cancellationToken))
            throw new InvalidOperationException($"A tenant named '{trimmed}' already exists.");

        entity.Name = trimmed;
        entity.IsActive = tenant.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Tenant does not exist.</exception>
    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {id} was not found.");

        if (entity.IsActive == isActive) return;

        entity.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            throw new ArgumentException("Tenant name is required.", nameof(name));

        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Tenant name cannot exceed {MaxNameLength} characters.", nameof(name));

        return trimmed;
    }
}