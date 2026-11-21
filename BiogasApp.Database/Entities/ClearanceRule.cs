using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class ClearanceRule
{
    public Guid Id { get; set; }
    public Guid RuleSetId { get; set; }
    public HazardClass FromHazardClass { get; set; }
    public HazardClass ToHazardClass { get; set; }
    public decimal MinDistanceM { get; set; }
    public decimal? CapacityMinL { get; set; }
    public decimal? CapacityMaxL { get; set; }
    public RuleStatus Status { get; set; }
    public string SourceNote { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public bool EngineerConfirmed { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public ClearanceRuleSet? RuleSet { get; set; }
}
