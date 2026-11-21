namespace BiogasApp.Model.DTOs;

public class AssessmentDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? EnquiryId { get; set; }
    public Guid? ProjectId { get; set; }

    // Draft or Converted
    public string Status { get; set; } = "Draft";

    // Expected CBG capacity in tonnes per day, used to scale the standard equipment sizes
    public double CapacityTpd { get; set; } = 12;

    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Plot, gate, north, wind, receptors and no-build zones.
    // Site.ProjectId carries the assessment id so the Site and Layout code can be reused.
    public SiteDto Site { get; set; } = new();

    public List<EquipmentItemDto> Equipment { get; set; } = new();
}
