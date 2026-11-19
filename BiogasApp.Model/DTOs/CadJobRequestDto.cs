namespace BiogasApp.Model.DTOs;

public class CadJobRequestDto
{
    public Guid SimulationRunId { get; set; }
    public List<string> ExportFormats { get; set; } = new();
    public DxfOptionsDto DxfOptions { get; set; } = new();
}

public class DxfOptionsDto
{
    public string CadVersion { get; set; } = "AutoCAD2018";
    public bool IncludeSafetyZones { get; set; } = true;
    public string DimensionUnit { get; set; } = "MM";
}