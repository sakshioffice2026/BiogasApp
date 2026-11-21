using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class AssessmentReceptor
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public ReceptorType Type { get; set; }
    public string? Name { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DistanceFromPlotM { get; set; }
    public ReceptorSource Source { get; set; }
    public bool IsConfirmed { get; set; }

    public SiteAssessment? Assessment { get; set; }
}
