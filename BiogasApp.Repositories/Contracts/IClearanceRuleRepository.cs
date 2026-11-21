using BiogasApp.Database.Entities;
using BiogasApp.Layout.Rules;

namespace BiogasApp.Repositories.Contracts;

public interface IClearanceRuleRepository
{
    Task<ClearanceRuleSet?> GetActiveRuleSetAsync(CancellationToken cancellationToken = default);

    Task<List<ClearanceRule>> GetRulesAsync(Guid ruleSetId, CancellationToken cancellationToken = default);

    Task<List<ClearanceSetting>> GetSettingsAsync(Guid ruleSetId, CancellationToken cancellationToken = default);

    Task<ClearanceRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClearanceSetting?> GetSettingAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateRuleAsync(ClearanceRule rule, CancellationToken cancellationToken = default);

    Task UpdateSettingAsync(ClearanceSetting setting, CancellationToken cancellationToken = default);

    Task ConfirmRuleAsync(Guid id, string confirmedBy, CancellationToken cancellationToken = default);

    Task ConfirmSettingAsync(Guid id, string confirmedBy, CancellationToken cancellationToken = default);

    // Loads the active rule set into memory for PlacementEngine and ClearanceChecker
    Task<IClearanceRuleProvider> CreateProviderAsync(
        Guid? ruleSetId = null,
        CancellationToken cancellationToken = default);
}