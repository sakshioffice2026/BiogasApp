using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface IAssessmentRepository
{
    Task<List<AssessmentListItemDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<AssessmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AssessmentDto> SaveAsync(AssessmentDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveReceptorsAsync(
        Guid assessmentId,
        IEnumerable<ReceptorDto> receptors,
        CancellationToken cancellationToken = default);

    Task SaveNoBuildZonesAsync(
        Guid assessmentId,
        IEnumerable<NoBuildZoneDto> zones,
        CancellationToken cancellationToken = default);

    Task<List<EquipmentItemDto>> GetEquipmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<EquipmentItemDto> SaveEquipmentItemAsync(
        EquipmentItemDto dto,
        CancellationToken cancellationToken = default);

    Task DeleteEquipmentItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task EnsureStandardEquipmentAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<List<LayoutDto>> GetLayoutVersionsAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<LayoutDto?> GetLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default);

    Task<LayoutDto?> GetLatestLayoutAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<LayoutDto> SaveLayoutAsync(LayoutDto dto, CancellationToken cancellationToken = default);

    Task DeleteLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default);

    Task<Guid> ConvertToProjectAsync(ConvertAssessmentDto request, CancellationToken cancellationToken = default);

    Task LinkEnquiryAsync(Guid assessmentId, Guid enquiryId, CancellationToken cancellationToken = default);

    Task<bool> HasApprovedProposalAsync(Guid assessmentId, CancellationToken cancellationToken = default);
}