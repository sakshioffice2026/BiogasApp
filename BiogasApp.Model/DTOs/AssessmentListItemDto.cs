namespace BiogasApp.Model.DTOs;

public class AssessmentListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? EnquiryId { get; set; }
    public Guid? ProjectId { get; set; }

    // Draft or Converted
    public string Status { get; set; } = "Draft";

    public double CapacityTpd { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double PlotAreaM2 { get; set; }

    public int ReceptorCount { get; set; }
    public int SensitiveReceptorCount { get; set; }
    public int LayoutVersionCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
