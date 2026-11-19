using BiogasApp.Model;

namespace BiogasApp.Repositories.Contracts;

public interface ITenant
{
    Task<List<TenantModel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TenantModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TenantModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<TenantModel> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task UpdateAsync(TenantModel tenant, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}