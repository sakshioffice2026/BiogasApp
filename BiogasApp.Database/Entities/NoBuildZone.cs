using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class NoBuildZone
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public NoBuildReason Reason { get; set; }
    public string PolygonJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public Project? Project { get; set; }
}
