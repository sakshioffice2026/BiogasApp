using BiogasApp.Database.Enums;

namespace BiogasApp.Layout.Rules;

public sealed record ClearanceRuleValue(
    double MinDistanceM, RuleStatus Status, string SourceNote);

public sealed record ClearanceSettingValue(
    string Key, double Value, string Unit, RuleStatus Status, string SourceNote);

public interface IClearanceRuleProvider
{
    Guid RuleSetId { get; }
    int RuleSetVersion { get; }

    ClearanceRuleValue? GetRule(HazardClass from, HazardClass to, double? capacityL = null);

    ClearanceSettingValue? GetSetting(string key);
}