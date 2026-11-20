using BiogasApp.Database.Entities;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IEnquiryInviteRepository
{
    /// <summary>Creates a new invite with a unique token, valid for 30 days and single use.</summary>
    Task<EnquiryInvite> CreateAsync(
        EnquiryInviteFormViewModel model,
        CancellationToken cancellationToken = default);

    /// <summary>Lists all invites, newest first. Pending invites past their expiry are marked Expired.</summary>
    Task<List<EnquiryInviteListItemViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks a token. IsValid is true only for a Pending, unexpired invite.</summary>
    Task<EnquiryInviteValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the invite Submitted and links it to the created enquiry.</summary>
    Task MarkSubmittedAsync(
        string token,
        Guid enquiryId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
