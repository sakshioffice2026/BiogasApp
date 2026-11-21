using BiogasApp.Layout.Geometry;

namespace BiogasApp.Layout.Models;

public sealed class LayoutResult
{
    public List<PlacedItem> Items { get; set; } = new();
    public List<List<Point2D>> Roads { get; set; } = new();
    public List<LayoutWarning> Warnings { get; set; } = new();
    public Point2D GatePoint { get; set; }
    public Guid? RuleSetId { get; set; }
    public int RuleSetVersion { get; set; }
    public bool IsDraft { get; set; } = true;
}