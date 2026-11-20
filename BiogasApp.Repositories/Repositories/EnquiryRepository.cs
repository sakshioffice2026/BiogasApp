using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class EnquiryRepository : IEnquiryRepository
{
    private readonly ApplicationDbContext _db;

    public EnquiryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EnquiryIndexViewModel> GetIndexAsync(
        string? search,
        EnquiryStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Enquiries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.EnquiryNo.Contains(term) ||
                e.CustomerName.Contains(term) ||
                (e.CompanyName != null && e.CompanyName.Contains(term)) ||
                e.Phone.Contains(term) ||
                e.LocationCity.Contains(term));
        }

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnquiryListItemViewModel
            {
                Id = e.Id,
                EnquiryNo = e.EnquiryNo,
                CustomerName = e.CustomerName,
                CompanyName = e.CompanyName,
                Phone = e.Phone,
                LocationCity = e.LocationCity,
                FeedstockType = e.FeedstockType,
                FeedstockQtyTpd = e.FeedstockQtyTpd,
                EndUse = e.EndUse,
                Source = e.Source,
                Status = e.Status,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new EnquiryIndexViewModel
        {
            SearchText = search,
            StatusFilter = status,
            Items = items
        };
    }

    public Task<Enquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Enquiries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Enquiry> AddAsync(Enquiry enquiry, CancellationToken cancellationToken = default)
    {
        if (enquiry.Id == Guid.Empty)
            enquiry.Id = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(enquiry.EnquiryNo))
            enquiry.EnquiryNo = await GenerateEnquiryNoAsync(cancellationToken);

        enquiry.CustomerName = enquiry.CustomerName.Trim();
        enquiry.CompanyName = enquiry.CompanyName?.Trim();
        enquiry.Email = enquiry.Email.Trim();
        enquiry.Phone = enquiry.Phone.Trim();
        enquiry.LocationCity = enquiry.LocationCity.Trim();
        enquiry.LocationState = enquiry.LocationState.Trim();
        enquiry.LocationCountry = enquiry.LocationCountry.Trim();
        enquiry.FeedstockType = enquiry.FeedstockType.Trim();
        enquiry.EndUse = enquiry.EndUse.Trim();
        enquiry.Status = EnquiryStatus.New;
        enquiry.CreatedAt = DateTime.UtcNow;

        _db.Enquiries.Add(enquiry);
        await _db.SaveChangesAsync(cancellationToken);

        return enquiry;
    }

    /// <exception cref="KeyNotFoundException">Enquiry does not exist.</exception>
    public async Task UpdateAsync(Enquiry enquiry, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Enquiries.FirstOrDefaultAsync(e => e.Id == enquiry.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Enquiry {enquiry.Id} was not found.");

        entity.CustomerName = enquiry.CustomerName.Trim();
        entity.CompanyName = enquiry.CompanyName?.Trim();
        entity.Email = enquiry.Email.Trim();
        entity.Phone = enquiry.Phone.Trim();
        entity.LocationCity = enquiry.LocationCity.Trim();
        entity.LocationState = enquiry.LocationState.Trim();
        entity.LocationCountry = enquiry.LocationCountry.Trim();
        entity.FeedstockType = enquiry.FeedstockType.Trim();
        entity.FeedstockQtyTpd = enquiry.FeedstockQtyTpd;
        entity.EndUse = enquiry.EndUse.Trim();
        entity.Message = enquiry.Message;
        entity.Source = enquiry.Source;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Enquiry does not exist.</exception>
    public async Task SetStatusAsync(
        Guid id,
        EnquiryStatus status,
        string? adminNotes,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Enquiries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Enquiry {id} was not found.");

        entity.Status = status;
        entity.AdminNotes = adminNotes;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GenerateEnquiryNoAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"ENQ-{year}-";

        var count = await _db.Enquiries
            .CountAsync(e => e.EnquiryNo.StartsWith(prefix), cancellationToken);

        return $"{prefix}{count + 1:D4}";
    }
}
