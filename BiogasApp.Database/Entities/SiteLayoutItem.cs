namespace BiogasApp.Database.Entities;

public class SiteLayoutItem
{
    public Guid Id { get; set; }
    public Guid SiteLayoutId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public int InstanceNo { get; set; } = 1;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsLocked { get; set; }

    public SiteLayout? SiteLayout { get; set; }
    public ProjectEquipmentItem? EquipmentItem { get; set; }
}
