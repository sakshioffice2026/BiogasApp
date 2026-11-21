using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class ClearanceSetting
{
    public Guid Id { get; set; }
    public Guid RuleSetId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public decimal ValueNum { get; set; }
    public string Unit { get; set; } = string.Empty;
    public RuleStatus Status { get; set; }
    public string SourceNote { get; set; } = string.Empty;
    public bool EngineerConfirmed { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public ClearanceRuleSet? RuleSet { get; set; }
}
