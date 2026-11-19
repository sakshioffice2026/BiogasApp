using BiogasApp.Database.Entities;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface ISimulationRepository
{
    Task<int> GetNextRunVersionAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<SimulationRun> AddRunAsync(
        SimulationRun run,
        IEnumerable<SizedEquipment> equipment,
        CancellationToken cancellationToken = default);

    Task<SimulationRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<SimulationRun?> GetLatestRunAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<SimulationRun>> GetRunsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<SizedEquipment>> GetEquipmentAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<SimulationResultDto?> GetResultDtoAsync(Guid runId, CancellationToken cancellationToken = default);
}