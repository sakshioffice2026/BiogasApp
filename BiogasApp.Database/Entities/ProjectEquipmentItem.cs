using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class ProjectEquipmentItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HazardClass HazardClass { get; set; }
    public EquipmentZone Zone { get; set; }
    public EquipmentShape Shape { get; set; }
    public decimal? WidthM { get; set; }
    public decimal? LengthM { get; set; }
    public decimal? DiameterM { get; set; }
    public decimal? HeightM { get; set; }
    public SizeSource SizeSource { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public bool IsCustom { get; set; }
    public bool AllowRotation { get; set; } = true;
    public bool NeedsRoadAccess { get; set; } = true;

    public Project? Project { get; set; }
}
