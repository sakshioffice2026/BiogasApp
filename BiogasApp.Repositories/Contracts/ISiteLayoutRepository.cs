using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface ISiteLayoutRepository
{
    Task<List<LayoutDto>> GetVersionsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<LayoutDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LayoutDto?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken = default);

    // Saves a new version (Auto or Manual) with items, warnings and roads; assigns VersionNo and rule set version
    Task<LayoutDto> SaveAsync(LayoutDto layout, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}