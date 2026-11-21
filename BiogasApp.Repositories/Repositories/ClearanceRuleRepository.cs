using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Rules;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class ClearanceRuleRepository : IClearanceRuleRepository
{
    private readonly ApplicationDbContext _db;

    public ClearanceRuleRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<ClearanceRuleSet?> GetActiveRuleSetAsync(CancellationToken cancellationToken = default) =>
        _db.ClearanceRuleSets.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<ClearanceRule>> GetRulesAsync(Guid ruleSetId, CancellationToken cancellationToken = default) =>
        _db.ClearanceRules.AsNoTracking()
            .Where(r => r.RuleSetId == ruleSetId)
            .OrderBy(r => r.FromHazardClass)
            .ThenBy(r => r.ToHazardClass)
            .ThenBy(r => r.CapacityMinL)
            .ToListAsync(cancellationToken);

    public Task<List<ClearanceSetting>> GetSettingsAsync(Guid ruleSetId, CancellationToken cancellationToken = default) =>
        _db.ClearanceSettings.AsNoTracking()
            .Where(s => s.RuleSetId == ruleSetId)
            .OrderBy(s => s.SettingKey)
            .ToListAsync(cancellationToken);

    public Task<ClearanceRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ClearanceRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ClearanceSetting?> GetSettingAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ClearanceSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task UpdateRuleAsync(ClearanceRule rule, CancellationToken cancellationToken = default)
    {
        var e = await _db.ClearanceRules.FirstOrDefaultAsync(r => r.Id == rule.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Clearance rule not found.");

        e.MinDistanceM = rule.MinDistanceM;
        e.CapacityMinL = rule.CapacityMinL;
        e.CapacityMaxL = rule.CapacityMaxL;
        e.Status = rule.Status;
        e.SourceNote = rule.SourceNote;
        e.SourceUrl = rule.SourceUrl;
        e.EngineerConfirmed = false;
        e.ConfirmedBy = null;
        e.ConfirmedAt = null;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSettingAsync(ClearanceSetting setting, CancellationToken cancellationToken = default)
    {
        var e = await _db.ClearanceSettings.FirstOrDefaultAsync(s => s.Id == setting.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Clearance setting not found.");

        e.ValueNum = setting.ValueNum;
        e.Unit = setting.Unit;
        e.Status = setting.Status;
        e.SourceNote = setting.SourceNote;
        e.EngineerConfirmed = false;
        e.ConfirmedBy = null;
        e.ConfirmedAt = null;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmRuleAsync(Guid id, string confirmedBy, CancellationToken cancellationToken = default)
    {
        var e = await _db.ClearanceRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Clearance rule not found.");

        e.EngineerConfirmed = true;
        e.ConfirmedBy = confirmedBy;
        e.ConfirmedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmSettingAsync(Guid id, string confirmedBy, CancellationToken cancellationToken = default)
    {
        var e = await _db.ClearanceSettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Clearance setting not found.");

        e.EngineerConfirmed = true;
        e.ConfirmedBy = confirmedBy;
        e.ConfirmedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IClearanceRuleProvider> CreateProviderAsync(
        Guid? ruleSetId = null,
        CancellationToken cancellationToken = default)
    {
        ClearanceRuleSet? set;
        if (ruleSetId.HasValue)
            set = await _db.ClearanceRuleSets.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == ruleSetId.Value, cancellationToken);
        else
            set = await GetActiveRuleSetAsync(cancellationToken);

        if (set == null)
            throw new InvalidOperationException("No clearance rule set found.");

        var rules = await GetRulesAsync(set.Id, cancellationToken);
        var settings = await GetSettingsAsync(set.Id, cancellationToken);

        return new InMemoryRuleProvider(set.Id, set.Version, rules, settings);
    }

    private sealed class InMemoryRuleProvider : IClearanceRuleProvider
    {
        private readonly List<ClearanceRule> _rules;
        private readonly Dictionary<string, ClearanceSetting> _settings;

        public Guid RuleSetId { get; }
        public int RuleSetVersion { get; }

        public InMemoryRuleProvider(
            Guid ruleSetId,
            int version,
            List<ClearanceRule> rules,
            List<ClearanceSetting> settings)
        {
            RuleSetId = ruleSetId;
            RuleSetVersion = version;
            _rules = rules;
            _settings = settings.ToDictionary(s => s.SettingKey, StringComparer.OrdinalIgnoreCase);
        }

        public ClearanceRuleValue? GetRule(HazardClass from, HazardClass to, double? capacityL = null)
        {
            // Rules are symmetric: A to B is the same as B to A
            var pair = _rules.Where(r =>
                (r.FromHazardClass == from && r.ToHazardClass == to) ||
                (r.FromHazardClass == to && r.ToHazardClass == from)).ToList();

            if (pair.Count == 0) return null;

            var ranged = pair.Where(r => r.CapacityMinL.HasValue || r.CapacityMaxL.HasValue).ToList();
            var flat = pair.Except(ranged).ToList();

            ClearanceRule? pick = null;

            if (capacityL.HasValue)
            {
                double c = capacityL.Value;
                pick = ranged
                    .Where(r => c >= (double)(r.CapacityMinL ?? 0m) &&
                                (!r.CapacityMaxL.HasValue || c <= (double)r.CapacityMaxL.Value))
                    .OrderByDescending(r => r.MinDistanceM)
                    .FirstOrDefault();
            }

            // No capacity known: use the most conservative (largest) distance
            pick ??= flat.OrderByDescending(r => r.MinDistanceM).FirstOrDefault()
                     ?? ranged.OrderByDescending(r => r.MinDistanceM).FirstOrDefault();

            return pick == null
                ? null
                : new ClearanceRuleValue((double)pick.MinDistanceM, pick.Status, pick.SourceNote);
        }

        public ClearanceSettingValue? GetSetting(string key) =>
            _settings.TryGetValue(key, out var s)
                ? new ClearanceSettingValue(s.SettingKey, (double)s.ValueNum, s.Unit, s.Status, s.SourceNote)
                : null;
    }
}