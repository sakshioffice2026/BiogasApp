namespace BiogasApp.Model.DTOs;

public class ReceptorDto
{
    public Guid? Id { get; set; }

    // School, Hospital, House, Road, Canal, PowerLine or Other
    public string Type { get; set; } = "Other";

    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceFromPlotM { get; set; }

    // MapData or UserAdded
    public string Source { get; set; } = "UserAdded";

    public bool IsConfirmed { get; set; }
}
