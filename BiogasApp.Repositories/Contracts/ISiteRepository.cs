using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface ISiteRepository
{
    Task<SiteDto?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<SiteDto> SaveAsync(SiteDto site, CancellationToken cancellationToken = default);

    Task SaveReceptorsAsync(
        Guid projectId,
        IEnumerable<ReceptorDto> receptors,
        CancellationToken cancellationToken = default);

    Task SaveNoBuildZonesAsync(
        Guid projectId,
        IEnumerable<NoBuildZoneDto> zones,
        CancellationToken cancellationToken = default);

    Task<List<EquipmentItemDto>> GetEquipmentAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<EquipmentItemDto> SaveEquipmentItemAsync(
        EquipmentItemDto item,
        CancellationToken cancellationToken = default);

    Task DeleteEquipmentItemAsync(Guid id, CancellationToken cancellationToken = default);
}