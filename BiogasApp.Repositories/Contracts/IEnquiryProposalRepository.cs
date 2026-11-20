using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IEnquiryProposalRepository
{
    /// <summary>Builds the proposal form for an enquiry, prefilled from the latest proposal when one exists.</summary>
    Task<EnquiryProposalFormViewModel?> GetFormAsync(
        Guid enquiryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the next proposal version: reuses or creates the pre-sales Project, saves the feedstock mix,
    /// runs the sizing engine, exports the PDF and BOM, and stores the proposal record.
    /// </summary>
    Task<EnquiryProposal> CreateAsync(
        EnquiryProposalFormViewModel model,
        CancellationToken cancellationToken = default);

    Task<EnquiryProposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EnquiryProposalDetailsViewModel?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<EnquiryProposalListItemViewModel>> GetByEnquiryAsync(
        Guid enquiryId,
        CancellationToken cancellationToken = default);

    Task SetStatusAsync(
        Guid id,
        ProposalStatus status,
        string? remarks,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the stored proposal PDF bytes, or null when no PDF exists.</summary>
    Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default);
}
