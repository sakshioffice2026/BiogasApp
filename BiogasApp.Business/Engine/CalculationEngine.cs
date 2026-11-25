namespace BiogasApp.Business.Engine;

public class CalculationEngine : ICalculationEngine
{
    private readonly FeedstockBlendingService _blending;
    private readonly DigesterSizingService _digester;
    private readonly GasYieldService _gas;
    private readonly HeatBalanceService _heat;
    private readonly MechanicalSizingService _mechanical;
    private readonly UpgradingService _upgrading;
    private readonly DigestateBomService _bom;
    private readonly PreTreatmentService _preTreatment;
    private readonly CbgDispatchService _dispatch;
    private readonly GasTreatmentService _gasTreatment;
    private readonly GasHolderService _gasHolder;
    private readonly PumpPipingService _pumpPiping;
    private readonly UtilitiesService _utilities;

    public CalculationEngine(
    FeedstockBlendingService blending,
    DigesterSizingService digester,
    GasYieldService gas,
    HeatBalanceService heat,
    MechanicalSizingService mechanical,
    UpgradingService upgrading,
    DigestateBomService bom,
    PreTreatmentService preTreatment,
    CbgDispatchService dispatch,
    GasTreatmentService gasTreatment,
    GasHolderService gasHolder,
    PumpPipingService pumpPiping,
    UtilitiesService utilities)
    {
        _blending = blending;
        _digester = digester;
        _gas = gas;
        _heat = heat;
        _mechanical = mechanical;
        _upgrading = upgrading;
        _bom = bom;
        _preTreatment = preTreatment;
        _dispatch = dispatch;
        _gasTreatment = gasTreatment;
        _gasHolder = gasHolder;
        _pumpPiping = pumpPiping;
        _utilities = utilities;
    }

    public CalculationResult Run(CalculationInput input)
    {
        // 3.1 Multi-feedstock blending
        var blending = _blending.Calculate(input.Feedstocks, input.TargetSlurryTsPercent);

        // 3.2 Digester hydraulic sizing
        var digester = _digester.Calculate(
            blending.VsTotalKgDay,
            blending.QSlurryM3Day,
            input.Regime);

        // 3.3 Gas yield
        var gas = _gas.Calculate(input.Feedstocks);

        // 3.4 Thermal heat balance (per tank)
        var heat = _heat.Calculate(
            blending.QSlurryM3Day / digester.NumTanks,
            digester.DiameterM,
            digester.TotalHeightM,
            digester.LiquidHeightM,
            HeatBalanceService.GetOperatingTempC(input.Regime),
            input.AmbientTempMinC);


        var heatSeasonal = _heat.CalculateSeasonal(
            blending.QSlurryM3Day / digester.NumTanks,
            digester.DiameterM,
            digester.TotalHeightM,
            digester.LiquidHeightM,
            HeatBalanceService.GetOperatingTempC(input.Regime));

        // 3.5 Mechanical sizing (per tank)
        var mechanical = _mechanical.Calculate(
            digester.DiameterM,
            digester.TotalHeightM,
            digester.LiquidHeightM,
            digester.VWorkingPerTankM3,
            blending.BlendedTsPct);

        // 3.6 Upgrading & compression
        var upgrading = _upgrading.Calculate(gas.QBiogasNm3Day, gas.Ch4Pct, input.UpgradingTech);

        // 3.7 Digestate & civil BOM
        var bom = _bom.Calculate(
            blending.QSlurryM3Day,
            blending.BlendedTsPct,
            digester.DiameterM,
            digester.TotalHeightM,
            digester.NumTanks);

        // 3.8 Pre-treatment (stage 2)
        var preTreatment = _preTreatment.Calculate(
            blending.MFeedTotalTpd,
            blending.MWaterTpd,
            blending.QSlurryM3Day);

        // 3.9 CBG dispatch (stage 6)
        var dispatch = _dispatch.Calculate(
            upgrading.MCbgKgDay,
            upgrading.CascadeWaterCapacityLiters);


        // 3.10 Gas treatment (H2S removal, chiller, booster, flare)
        var gasTreatment = _gasTreatment.Calculate(gas.QBiogasNm3Day, gas.MH2sKgDay);

        // 3.11 Gas holder and dome
        var gasHolder = _gasHolder.Calculate(
            gas.QBiogasNm3Day,
            digester.DiameterM,
            digester.NumTanks);

        // 3.12 Pumps and piping
        var pumpPiping = _pumpPiping.Calculate(
            blending.QSlurryM3Day,
            gas.QBiogasNm3Hr);

        var parasiticHeatKw = heat.QTotalThermalDutyKw * digester.NumTanks;
        var parasiticPowerKw =
        (mechanical.TotalMixingPowerKw * digester.NumTanks) +
        upgrading.UpgradingPowerKw +
        upgrading.CompressorPowerKw +
        preTreatment.ShredderPowerKw +
        preTreatment.MixerPowerKw +
        preTreatment.FeedTankMixerPowerKw +
        gasTreatment.BoosterPowerKw +
        pumpPiping.FeedPump.PowerKw +
        pumpPiping.DigestatePump.PowerKw;

        // 3.13 Utilities (process water, fire water, hot water generator, MCC, transformer, DG)
        var utilities = _utilities.Calculate(
            preTreatment.DilutionWaterM3Day,
            parasiticPowerKw,
            parasiticHeatKw);

        return new CalculationResult(
    Blending: blending,
    Digester: digester,
    Gas: gas,
    Heat: heat,
    Mechanical: mechanical,
    Upgrading: upgrading,
    Bom: bom,
    PreTreatment: preTreatment,
    Dispatch: dispatch,
    ParasiticHeatKw: parasiticHeatKw,
    ParasiticPowerKw: parasiticPowerKw,
    Warnings: blending.Warnings,
    HeatSeasonal: heatSeasonal,
    GasTreatment: gasTreatment,
    GasHolder: gasHolder,
    PumpPiping: pumpPiping,
    Utilities: utilities);
    }
}