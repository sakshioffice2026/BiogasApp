namespace BiogasApp.Model.DTOs;

public class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class LatLonDto
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class NoBuildZoneDto
{
    public Guid? Id { get; set; }
    public string Reason { get; set; } = "Other";
    public List<PointDto> Polygon { get; set; } = new();
    public string? Notes { get; set; }
}

public class SiteDto
{
    public Guid ProjectId { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Map, Typed or Rectangle
    public string InputMode { get; set; } = "Map";

    // Corner points in metres, local to the plot origin (used by Typed and for saved plots)
    public List<PointDto> PlotCorners { get; set; } = new();

    // Corner points drawn on the satellite map (used by Map mode)
    public List<LatLonDto> PlotCornersLatLon { get; set; } = new();

    // Used by Rectangle mode
    public double? RectLengthM { get; set; }
    public double? RectWidthM { get; set; }

    public double PlotAreaM2 { get; set; }

    public int GateEdgeIndex { get; set; }
    public double GateOffsetM { get; set; }
    public double NorthAngleDeg { get; set; }
    public double? WindFromDeg { get; set; }
    public double? SlopeDirectionDeg { get; set; }
    public double? PowerSideDeg { get; set; }
    public double? GasOfftakeSideDeg { get; set; }
    public string? Notes { get; set; }

    public List<NoBuildZoneDto> NoBuildZones { get; set; } = new();
    public List<ReceptorDto> Receptors { get; set; } = new();
}
