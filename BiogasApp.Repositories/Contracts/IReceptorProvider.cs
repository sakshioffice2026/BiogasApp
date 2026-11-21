using BiogasApp.Model.DTOs;

namespace BiogasApp.Repositories.Contracts;

public interface IReceptorProvider
{
    Task<List<ReceptorDto>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusM,
        CancellationToken cancellationToken = default);
}