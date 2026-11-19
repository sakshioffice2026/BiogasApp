namespace BiogasApp.Database.Entities;

public class SimulationRun
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int RunVersion { get; set; } = 1;
    public decimal TotalFeedTpd { get; set; }
    public decimal BlendedTsPercent { get; set; }
    public decimal BlendedVsTpd { get; set; }
    public decimal BlendedCnRatio { get; set; }
    public decimal HrtDays { get; set; }
    public decimal OlrKgVsM3Day { get; set; }
    public decimal RawBiogasNm3Day { get; set; }
    public decimal Ch4Percent { get; set; }
    public decimal Co2Percent { get; set; }
    public decimal H2sPpm { get; set; }
    public decimal NetCbgKgDay { get; set; }
    public decimal ParasiticHeatKw { get; set; }
    public decimal ParasiticPowerKw { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public ICollection<SizedEquipment> Equipment { get; set; } = new List<SizedEquipment>();
    public ICollection<GeneratedArtifact> Artifacts { get; set; } = new List<GeneratedArtifact>();
    public ICollection<CadJob> CadJobs { get; set; } = new List<CadJob>();
}