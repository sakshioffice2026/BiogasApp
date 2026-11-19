using BiogasApp.Database.Entities;

namespace BiogasApp.Repositories.Contracts;

public interface IRateCardRepository
{
    Task<List<VendorRateCard>> GetAllAsync(
        Guid tenantId,
        string? category = null,
        CancellationToken cancellationToken = default);

    Task<VendorRateCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<VendorRateCard?> GetByItemCodeAsync(
        Guid tenantId,
        string itemCode,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, decimal>> GetRateMapAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<VendorRateCard> AddAsync(VendorRateCard rateCard, CancellationToken cancellationToken = default);

    Task UpdateAsync(VendorRateCard rateCard, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}