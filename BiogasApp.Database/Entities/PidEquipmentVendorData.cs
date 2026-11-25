namespace BiogasApp.Database.Entities;

public class PidEquipmentVendorData
{
    public Guid Id { get; set; }
    public Guid PidDiagramId { get; set; }
    public string Tag { get; set; } = string.Empty;

    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Material { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public int? DeliveryWeeks { get; set; }
    public string? Remarks { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PidDiagram? PidDiagram { get; set; }
}
