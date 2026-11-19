namespace BiogasApp.Model.DTOs;

public class DashboardDto
{
    public Guid SimulationRunId { get; set; }
    public double RawBiogasNm3Day { get; set; }
    public double NetCbgKgDay { get; set; }
    public int DigesterCount { get; set; }
    public double DigesterDiameterM { get; set; }
    public double DigesterHeightM { get; set; }
    public double DigesterVolumeM3 { get; set; }
    public double ParasiticPowerKw { get; set; }
    public double ParasiticHeatKw { get; set; }
    public double BlendedCnRatio { get; set; }
    public string CnGaugeZone { get; set; } = "Green";
    public List<string> Warnings { get; set; } = new();
    public List<TechnologyComparisonDto> TechnologyComparison { get; set; } = new();
}

public class TechnologyComparisonDto
{
    public string Technology { get; set; } = string.Empty;
    public double RecoveryPercent { get; set; }
    public double PurityPercent { get; set; }
    public double UpgradingPowerKw { get; set; }
    public double CompressorPowerKw { get; set; }
    public double NetCbgKgDay { get; set; }
}