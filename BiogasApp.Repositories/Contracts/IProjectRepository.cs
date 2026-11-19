using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IProjectRepository
{
    Task<ProjectIndexViewModel> GetIndexAsync(
        string? search,
        ProjectStatus? status,
        CancellationToken cancellationToken = default);

    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);

    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetStatusAsync(Guid id, ProjectStatus status, CancellationToken cancellationToken = default);

    Task<List<ProjectFeedstockMix>> GetFeedstockMixAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveFeedstockMixAsync(
        Guid projectId,
        IEnumerable<ProjectFeedstockMix> mix,
        CancellationToken cancellationToken = default);
}