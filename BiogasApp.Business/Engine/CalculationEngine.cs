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

    public CalculationEngine(
        FeedstockBlendingService blending,
        DigesterSizingService digester,
        GasYieldService gas,
        HeatBalanceService heat,
        MechanicalSizingService mechanical,
        UpgradingService upgrading,
        DigestateBomService bom,
        PreTreatmentService preTreatment,
        CbgDispatchService dispatch)
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

        var parasiticHeatKw = heat.QTotalThermalDutyKw * digester.NumTanks;
        var parasiticPowerKw =
            (mechanical.TotalMixingPowerKw * digester.NumTanks) +
            upgrading.UpgradingPowerKw +
            upgrading.CompressorPowerKw;

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
            Warnings: blending.Warnings);
    }
}