namespace BiogasApp.Business.Constants;

public static class EngineeringConstants
{
    // ---------- 3.1 Blending ----------
    public const double DefaultTargetSlurryTsPercent = 9.0;
    public const double SlurryDensityTPerM3 = 1.02;
    public const double CnCriticalMin = 15.0;
    public const double CnCriticalMax = 35.0;
    public const double CnOptimalMin = 20.0;
    public const double CnOptimalMax = 30.0;
    public const double FanToxicityCnThreshold = 12.0;

    // ---------- 3.2 Digester sizing ----------
    public const double HrtMesophilicDays = 30.0;
    public const double HrtThermophilicDays = 20.0;
    public const double OlrMesophilicKgVsM3Day = 3.5;
    public const double OlrThermophilicKgVsM3Day = 5.0;
    public const double HeadspaceFactor = 0.12;
    public const double MaxSingleTankVolumeM3 = 6800.0;
    public const double DigesterAspectRatioHd = 0.29;
    public const double DiameterRoundingStepM = 0.5;

    // ---------- 3.3 Gas yield ----------
    public const double DefaultDegradationFactor = 0.85;
    public const double MethaneFraction = 0.55;
    public const double MinorGasesPercent = 0.5;
    public const double H2sPpmAgro = 2500.0;
    public const double H2sPpmChickenLitter = 4000.0;
    public const double H2sPpmDefault = 1500.0;
    public const double H2sKgPerNm3 = 1.52;
    public const string ChickenLitterName = "Chicken Litter";
    public const string AgroCategoryName = "Agro";

    // ---------- 3.4 Thermal ----------
    public const double MesophilicOperatingTempC = 37.0;
    public const double ThermophilicOperatingTempC = 55.0;
    public const double DefaultAmbientMinTempC = 10.0;
    public const double SlurryHeatDensityKgM3 = 1020.0;
    public const double SlurrySpecificHeatKjPerKgK = 4.18;
    public const double UFloorWPerM2K = 0.60;
    public const double UWallWPerM2K = 0.40;
    public const double URoofWPerM2K = 1.20;
    public const double RoofAreaFactor = 1.15;
    public const double HeatSafetyFactor = 1.15;
    public const double HxLmtdC = 37.88;
    public const double HxUWPerM2K = 850.0;
    public const double HxAreaMargin = 1.1;

    // ---------- 3.5 Mechanical & shell ----------
    public const double MixingPowerDensityHighTsWPerM3 = 12.0;
    public const double MixingPowerDensityLowTsWPerM3 = 8.5;
    public const double MixingTsThresholdPercent = 8.0;
    public const double AgitatorDiameterLimit2M = 18.0;
    public const double AgitatorDiameterLimit3M = 26.0;
    public const double ShellAllowableStressMpa = 160.0;
    public const double ShellJointEfficiency = 0.85;
    public const double ShellSlurrySpecificGravity = 1.02;
    public const double ShellCorrosionAllowanceMm = 2.0;
    public const double ShellCourseHeightM = 2.0;
    public const double ShellMinThicknessMm = 6.0;

    public static readonly int[] CommercialThicknessesMm = { 6, 8, 10, 12, 14, 16, 20, 22, 25 };

    // ---------- 3.6 Upgrading & compression ----------
    public const double CbgDensityKgPerNm3 = 0.72;
    public const double CompressorSpecificEnergyKwhPerNm3 = 0.25;
    public const double CompressorSurgeFactor = 1.2;
    public const double CascadeBufferFraction = 0.50;
    public const double CascadePressureBar = 250.0;
    public const double CascadeCompressibilityZ = 0.85;
    public const double StandardPressureBar = 1.01325;
    public const double StandardTempK = 273.15;
    public const double CascadeActualTempK = 293.15;
    public const double CascadeCylinderLiters = 3000.0;

    // ---------- 3.7 Digestate & civil ----------
    public const double DigestateFlowFactor = 0.92;
    public const double DigestateDensityTPerM3 = 1.01;
    public const double DigestateTsRetainedFraction = 0.60;
    public const double SolidCakeTsFraction = 0.30;
    public const double LiquidFiltrateTsFraction = 0.03;
    public const double RccWallThicknessM = 0.40;
    public const double RaftThicknessM = 0.50;
    public const double RaftOffsetM = 0.5;
    public const double ExcavationOffsetM = 1.5;
    public const double ExcavationDepthM = 2.5;
    public const double RebarKgPerM3Concrete = 120.0;


    // ---------- Stage 2 Pre-treatment ----------
    public const double ShredderOperatingHoursPerDay = 16.0;
    public const double ShredderCapacityFactor = 1.25;
    public const double ShredderSpecificEnergyKwhPerTon = 4.0;
    public const double MixTankResidenceHours = 2.0;
    public const double MixTankPowerDensityWPerM3 = 8.0;
    public const double WaterDensityTPerM3 = 1.0;

    // ---------- Stage 6 CBG dispatch ----------
    public const double CbgTankerPayloadKg = 1000.0;
    public const double PlantOperatingDaysPerYear = 330.0;
    public static readonly double CascadeStoredKgPerM3Water =
        (CascadePressureBar * StandardTempK) /
        (StandardPressureBar * CascadeCompressibilityZ * CascadeActualTempK) *
        CbgDensityKgPerNm3;

    // ---------- 5. DXF shell development ----------
    public const double SheetLengthMm = 8000.0;
    public const double SheetHeightMm = 2000.0;
    public const double BevelThicknessLimitMm = 12.0;
    public const double BevelAngleThinDeg = 30.0;
    public const double BevelAngleThickDeg = 37.5;
    public const double WeldRootFaceMm = 1.5;
    public const double ShellLengthToleranceMm = 0.5;

    // ---------- Regulatory clearances ----------
    public const double FlareToGasStorageMinM = 30.0;
    public const double CascadeToBoundaryMinM = 15.0;
    public const double DigesterWallToWallMinM = 5.0;
    public const double OfficeControlMinM = 15.0;
    public const double AtexZone1RadiusM = 3.0;
    public const double MinPlotAreaM2 = 2500.0;
}