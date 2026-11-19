using BiogasApp.Database;
using BiogasApp.Model;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class AdminRepository : IAdminRepository
{
    private const int OverviewExceptionCount = 20;
    private const int MaxExceptionCount = 500;

    private readonly ApplicationDbContext _db;

    public AdminRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// tenantId = null  -> platform-wide view (all users, all rate cards, recent exceptions).
    /// tenantId = value -> that tenant's users and rate cards only. Exceptions are not
    ///                     tenant-scoped, so they are left out of a tenant view.
    /// </summary>
    public async Task<AdminViewModel> GetOverviewAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var users = await GetUsersAsync(tenantId, cancellationToken);

        var rateCardsQuery = _db.VendorRateCards.AsNoTracking();
        if (tenantId.HasValue)
            rateCardsQuery = rateCardsQuery.Where(r => r.TenantId == tenantId.Value);

        var rateCards = await rateCardsQuery
            .OrderBy(r => r.Category)
            .ThenBy(r => r.ItemCode)
            .Select(r => new RateCardViewModel
            {
                Id = r.Id,
                ItemCode = r.ItemCode,
                Category = r.Category,
                Description = r.Description,
                Unit = r.Unit,
                UnitRate = r.UnitRate,
                Currency = r.Currency
            })
            .ToListAsync(cancellationToken);

        var exceptions = tenantId.HasValue
            ? new List<ExceptionLogItemViewModel>()
            : await GetRecentExceptionsAsync(OverviewExceptionCount, cancellationToken);

        return new AdminViewModel
        {
            Users = users,
            RateCards = rateCards,
            RecentExceptions = exceptions
        };
    }

    public Task<List<AdminUserViewModel>> GetUsersAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AppUsers.AsNoTracking();

        if (tenantId.HasValue)
            query = query.Where(u => u.TenantId == tenantId.Value);

        return query
            .OrderBy(u => u.Tenant!.Name)
            .ThenBy(u => u.FullName)
            .Select(u => new AdminUserViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = u.Role!.Name,
                TenantName = u.Tenant!.Name,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<UserRoleModel>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        _db.UserRoles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new UserRoleModel { Id = r.Id, Name = r.Name })
            .ToListAsync(cancellationToken);

    /// <exception cref="KeyNotFoundException">User does not exist.</exception>
    public async Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (user.IsActive == isActive) return;

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">User or role does not exist.</exception>
    public async Task ChangeUserRoleAsync(Guid userId, int roleId, CancellationToken cancellationToken = default)
    {
        if (!await _db.UserRoles.AnyAsync(r => r.Id == roleId, cancellationToken))
            throw new KeyNotFoundException($"Role {roleId} was not found.");

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} was not found.");

        if (user.RoleId == roleId) return;

        user.RoleId = roleId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ExceptionLogItemViewModel>> GetRecentExceptionsAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(count, 1, MaxExceptionCount);

        return _db.ExceptionLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new ExceptionLogItemViewModel
            {
                Id = x.Id,
                Message = x.Message,
                Source = x.Source,
                RequestPath = x.RequestPath,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}