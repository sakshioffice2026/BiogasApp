using BiogasApp.Database.Enums;

namespace BiogasApp.Layout.Models;

public sealed class LayoutWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RuleStatus? RuleStatus { get; set; }
    public string? SourceNote { get; set; }
    public Guid? EquipmentId { get; set; }
    public int? InstanceNo { get; set; }
    public Guid? OtherEquipmentId { get; set; }
    public double? ActualDistanceM { get; set; }
    public double? RequiredDistanceM { get; set; }
}