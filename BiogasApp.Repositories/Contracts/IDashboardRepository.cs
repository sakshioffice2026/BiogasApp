using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface IDashboardRepository
{
    Task<DashboardDto?> GetDashboardAsync(Guid simulationRunId, CancellationToken cancellationToken = default);

    Task<DashboardDto?> GetLatestDashboardAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Dictionary<ProjectStatus, int>> GetProjectStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<int> GetTotalRunCountAsync(CancellationToken cancellationToken = default);
}