using System.Linq.Expressions;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Model;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using BiogasApp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class AccountRepository : IAccount
{
    /// <summary>Role given to the first user of a newly registered tenant. Must exist in user_roles.</summary>
    public const string DefaultRegistrationRole = "ClientAdmin";

    private static readonly Expression<Func<AppUser, AppUserModel>> ToModel = u => new AppUserModel
    {
        Id = u.Id,
        TenantId = u.TenantId,
        TenantName = u.Tenant!.Name,
        RoleId = u.RoleId,
        RoleName = u.Role!.Name,
        FullName = u.FullName,
        Email = u.Email,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };

    private static readonly Func<AppUser, AppUserModel> Map = ToModel.Compile();

    private readonly ApplicationDbContext _db;

    public AccountRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppUserModel?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);

        var user = await _db.AppUsers
            .AsNoTracking()
            .Include(u => u.Tenant)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (user is null || !user.IsActive || user.Tenant is null || !user.Tenant.IsActive)
            return null;

        return Encryp_Decrypt.Encryptdata(password) == user.PasswordHash ? Map(user) : null;
    }

    /// <summary>
    /// Creates a new tenant and its first user (role = <see cref="DefaultRegistrationRole"/>).
    /// Returns null when the email is already registered or the tenant name is already taken.
    /// Throws InvalidOperationException if the role row does not exist in user_roles.
    /// </summary>
    public async Task<AppUserModel?> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(model.Email);
        var tenantName = model.TenantName.Trim();

        if (await _db.AppUsers.AnyAsync(u => u.Email == email, cancellationToken))
            return null;

        if (await _db.Tenants.AnyAsync(t => t.Name == tenantName, cancellationToken))
            return null;

        var role = await _db.UserRoles
            .FirstOrDefaultAsync(r => r.Name == DefaultRegistrationRole, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Role '{DefaultRegistrationRole}' was not found in user_roles. Insert it before registering users.");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = tenantName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            RoleId = role.Id,
            FullName = model.FullName.Trim(),
            Email = email,
            PasswordHash = Encryp_Decrypt.Encryptdata(model.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Tenant = tenant,
            Role = role
        };

        _db.Tenants.Add(tenant);
        _db.AppUsers.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Most likely a concurrent registration hit the unique email index.
            _db.Entry(user).State = EntityState.Detached;
            _db.Entry(tenant).State = EntityState.Detached;
            return null;
        }

        return Map(user);
    }

    public Task<AppUserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.AppUsers
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(ToModel)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AppUserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);

        return _db.AppUsers
            .AsNoTracking()
            .Where(u => u.Email == normalized)
            .Select(ToModel)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        return _db.AppUsers.AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive || Encryp_Decrypt.Encryptdata(currentPassword) != user.PasswordHash)
            return false;

        user.PasswordHash = Encryp_Decrypt.Encryptdata(newPassword);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}