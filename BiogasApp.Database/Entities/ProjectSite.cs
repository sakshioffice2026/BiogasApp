namespace BiogasApp.Database.Entities;

public class ProjectSite
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string PlotCornersJson { get; set; } = "[]";
    public decimal PlotAreaM2 { get; set; }
    public int GateEdgeIndex { get; set; }
    public decimal GateOffsetM { get; set; }
    public decimal NorthAngleDeg { get; set; }
    public decimal? WindFromDeg { get; set; }
    public decimal? SlopeDirectionDeg { get; set; }
    public decimal? PowerSideDeg { get; set; }
    public decimal? GasOfftakeSideDeg { get; set; }
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Project? Project { get; set; }
}
