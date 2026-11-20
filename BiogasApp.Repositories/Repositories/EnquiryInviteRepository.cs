using System.Security.Cryptography;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Business.Financial;

/// <summary>
/// Personal customer invite links: unique token, valid for 30 days, single use.
///
/// Register:  services.AddScoped&lt;IEnquiryInviteRepository, EnquiryInviteRepository&gt;();
/// </summary>
public class EnquiryInviteRepository : IEnquiryInviteRepository
{
    private const int ExpiryDays = 30;

    private readonly ApplicationDbContext _db;

    public EnquiryInviteRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EnquiryInvite> CreateAsync(
        EnquiryInviteFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var invite = new EnquiryInvite
        {
            Id = Guid.NewGuid(),
            Token = GenerateToken(),
            CustomerName = model.CustomerName.Trim(),
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            Status = InviteStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddDays(ExpiryDays)
        };

        _db.Set<EnquiryInvite>().Add(invite);
        await _db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<List<EnquiryInviteListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var stale = await _db.Set<EnquiryInvite>()
            .Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (stale.Count > 0)
        {
            foreach (var invite in stale)
            {
                invite.Status = InviteStatus.Expired;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return await _db.Set<EnquiryInvite>()
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new EnquiryInviteListItemViewModel
            {
                Id = i.Id,
                Token = i.Token,
                CustomerName = i.CustomerName,
                Phone = i.Phone,
                Email = i.Email,
                Status = i.Status,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                SubmittedAt = i.SubmittedAt,
                EnquiryId = i.EnquiryId,
                EnquiryNo = i.Enquiry != null ? i.Enquiry.EnquiryNo : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<EnquiryInviteValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new EnquiryInviteValidationResult { IsValid = false };
        }

        var invite = await _db.Set<EnquiryInvite>()
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

        if (invite == null || invite.Status != InviteStatus.Pending)
        {
            return new EnquiryInviteValidationResult { IsValid = false };
        }

        if (invite.ExpiresAt < DateTime.UtcNow)
        {
            invite.Status = InviteStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            return new EnquiryInviteValidationResult { IsValid = false };
        }

        return new EnquiryInviteValidationResult
        {
            IsValid = true,
            CustomerName = invite.CustomerName,
            Phone = invite.Phone,
            Email = invite.Email
        };
    }

    public async Task MarkSubmittedAsync(
        string token,
        Guid enquiryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var invite = await _db.Set<EnquiryInvite>()
            .FirstOrDefaultAsync(i => i.Token == token && i.Status == InviteStatus.Pending, cancellationToken);

        if (invite == null)
        {
            return;
        }

        invite.Status = InviteStatus.Submitted;
        invite.SubmittedAt = DateTime.UtcNow;
        invite.EnquiryId = enquiryId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invite = await _db.Set<EnquiryInvite>()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invite == null)
        {
            throw new KeyNotFoundException("Invite not found.");
        }

        if (invite.Status != InviteStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invites can be cancelled.");
        }

        invite.Status = InviteStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
