namespace BiogasApp.Database.Entities;

public class AssessmentLayoutItem
{
    public Guid Id { get; set; }
    public Guid AssessmentLayoutId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public int InstanceNo { get; set; } = 1;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal RotationDeg { get; set; }
    public bool IsLocked { get; set; }

    public AssessmentLayout? AssessmentLayout { get; set; }
    public AssessmentEquipmentItem? EquipmentItem { get; set; }
}
