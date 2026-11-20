using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IEnquiryRepository
{
    Task<EnquiryIndexViewModel> GetIndexAsync(
        string? search,
        EnquiryStatus? status,
        CancellationToken cancellationToken = default);

    Task<Enquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Enquiry> AddAsync(Enquiry enquiry, CancellationToken cancellationToken = default);

    Task UpdateAsync(Enquiry enquiry, CancellationToken cancellationToken = default);

    Task SetStatusAsync(
        Guid id,
        EnquiryStatus status,
        string? adminNotes,
        CancellationToken cancellationToken = default);

    Task<string> GenerateEnquiryNoAsync(CancellationToken cancellationToken = default);
}
