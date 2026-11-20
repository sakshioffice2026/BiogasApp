using BiogasApp.Business.Constants;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Engine;

public record CalculationInput(
    IReadOnlyList<FeedstockInput> Feedstocks,
    DigesterRegime Regime,
    UpgradingTech UpgradingTech,
    double AmbientTempMinC = EngineeringConstants.DefaultAmbientMinTempC,
    double TargetSlurryTsPercent = EngineeringConstants.DefaultTargetSlurryTsPercent);

public record CalculationResult(
    BlendingResult Blending,
    DigesterSizingResult Digester,
    GasYieldResult Gas,
    HeatBalanceResult Heat,
    MechanicalSizingResult Mechanical,
    UpgradingResult Upgrading,
    DigestateBomResult Bom,
    PreTreatmentResult PreTreatment,
    CbgDispatchResult Dispatch,
    double ParasiticHeatKw,
    double ParasiticPowerKw,
    IReadOnlyList<string> Warnings);

public interface ICalculationEngine
{
    CalculationResult Run(CalculationInput input);
}