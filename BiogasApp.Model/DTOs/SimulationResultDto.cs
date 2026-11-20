namespace BiogasApp.Model.DTOs;

public class SimulationResultDto
{
    public Guid SimulationRunId { get; set; }
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public MassBalanceDto MassBalance { get; set; } = new();
    public DigesterKineticsDto DigesterKinetics { get; set; } = new();
    public PreTreatmentDto PreTreatment { get; set; } = new();
    public ThermalMechanicalDto Thermal { get; set; } = new();
    public UpgradingDto Upgrading { get; set; } = new();
    public DigestateCivilDto Digestate { get; set; } = new();
    public CbgDispatchDto Dispatch { get; set; } = new();
    public List<EquipmentSizingDto> EquipmentSizing { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class PreTreatmentDto
{
    public double ShredderCapacityTph { get; set; }
    public double ShredderPowerKw { get; set; }
    public double MixTankVolumeM3 { get; set; }
    public double MixerPowerKw { get; set; }
    public double DilutionWaterM3Day { get; set; }
}

public class ShellCourseDto
{
    public int CourseIndex { get; set; }
    public double CalculatedMm { get; set; }
    public double SpecifiedMm { get; set; }
}

public class ThermalMechanicalDto
{
    public int TankCount { get; set; }
    public double FeedHeatingKwPerTank { get; set; }
    public double TransmissionLossKwPerTank { get; set; }
    public double TotalThermalDutyKwPerTank { get; set; }
    public double TotalThermalDutyKw { get; set; }
    public double HeatExchangerAreaM2PerTank { get; set; }
    public double TotalMixingPowerKwPerTank { get; set; }
    public int AgitatorCountPerTank { get; set; }
    public double PowerPerAgitatorKw { get; set; }
    public List<ShellCourseDto> ShellCourses { get; set; } = new();
}

public class UpgradingDto
{
    public string Technology { get; set; } = string.Empty;
    public double CbgNm3Day { get; set; }
    public double CbgKgDay { get; set; }
    public double UpgradingPowerKw { get; set; }
    public double CompressorPowerKw { get; set; }
    public double CompressorCapacityNm3Hr { get; set; }
    public double CascadeWaterCapacityLiters { get; set; }
    public int CascadeBanks3000L { get; set; }
}

public class DigestateCivilDto
{
    public double SolidCakeTpd { get; set; }
    public double LiquidLfomTpd { get; set; }
    public double ExcavationM3 { get; set; }
    public double RccConcreteM3 { get; set; }
    public double SteelRebarTons { get; set; }
}

public class CbgDispatchDto
{
    public double CbgTonnesPerDay { get; set; }
    public double CbgTonnesPerYear { get; set; }
    public double TankerPayloadKg { get; set; }
    public int TankerLoadsPerDay { get; set; }
    public double FillTimeHoursPerTanker { get; set; }
    public double BufferHoursAvailable { get; set; }
}