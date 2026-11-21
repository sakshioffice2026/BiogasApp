using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class SiteReceptor
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ReceptorType Type { get; set; }
    public string? Name { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DistanceFromPlotM { get; set; }
    public ReceptorSource Source { get; set; }
    public bool IsConfirmed { get; set; }

    public Project? Project { get; set; }
}
