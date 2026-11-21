namespace BiogasApp.Model.DTOs;

public class EquipmentItemDto
{
    public Guid? Id { get; set; }
    public Guid ProjectId { get; set; }

    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Cascade, Compressor, Dispenser, GasVessel, DigesterGas, Scrubber, Flare,
    // Electrical, Building, Road, Boundary, Receptor, Tank, OpenYard or Other
    public string HazardClass { get; set; } = "Other";

    // Dirty, Process, Gas or Clean
    public string Zone { get; set; } = "Process";

    // Circle or Rectangle
    public string Shape { get; set; } = "Rectangle";

    public double? WidthM { get; set; }
    public double? LengthM { get; set; }
    public double? DiameterM { get; set; }

    // Simulation, Standard or UserOverride
    public string SizeSource { get; set; } = "Standard";

    public int Quantity { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public bool IsCustom { get; set; }
    public bool AllowRotation { get; set; } = true;
    public bool NeedsRoadAccess { get; set; } = true;
}
