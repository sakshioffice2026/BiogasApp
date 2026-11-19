using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;

namespace BiogasApp.Repositories.Contracts;

public interface IFeedstockRepository
{
    Task<List<Feedstock>> GetAllAsync(
        string? search = null,
        FeedstockCategory? category = null,
        CancellationToken cancellationToken = default);

    Task<Feedstock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Feedstock>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<Feedstock> AddAsync(Feedstock feedstock, CancellationToken cancellationToken = default);

    Task UpdateAsync(Feedstock feedstock, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}