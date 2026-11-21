using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class ClearanceRuleSet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RegulatoryStandard RegulatoryStandard { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClearanceRule> Rules { get; set; } = new List<ClearanceRule>();
    public ICollection<ClearanceSetting> Settings { get; set; } = new List<ClearanceSetting>();
}
