using BiogasApp.Database.Entities;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface ICadGeneration
{
    Task<CadJobStatusDto> QueueJobAsync(CadJobRequestDto request, CancellationToken cancellationToken = default);

    Task<CadJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<List<CadJob>> GetJobsByRunAsync(Guid simulationRunId, CancellationToken cancellationToken = default);

    Task<ArtifactListDto> GetArtifactsAsync(Guid simulationRunId, CancellationToken cancellationToken = default);

    Task<ArtifactDto?> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default);
}