using BiogasApp.Business.Constants;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Engine;

public record UpgradingResult(
    double QCbgNm3Day,
    double MCbgKgDay,
    double UpgradingPowerKw,
    double CompressorPowerKw,
    double CompressorCapacityNm3Hr,
    double CascadeWaterCapacityLiters,
    int NumCascadeBanks3000L);

public class UpgradingService
{
    public UpgradingResult Calculate(
        double qBiogasNm3Day,
        double ch4Pct,
        UpgradingTech upgradingTech = UpgradingTech.Psa)
    {
        var spec = UpgradingSpecs.Get(upgradingTech);

        var qBiogasNm3Hr = qBiogasNm3Day / 24.0;
        var qCh4In = qBiogasNm3Day * (ch4Pct / 100.0);
        var qCh4Recovered = qCh4In * spec.Recovery;

        // CBG product flow
        var qCbgNm3Day = qCh4Recovered / spec.Purity;
        var qCbgNm3Hr = qCbgNm3Day / 24.0;
        var mCbgKgDay = qCbgNm3Day * EngineeringConstants.CbgDensityKgPerNm3;

        var pUpgradingKw = qBiogasNm3Hr * spec.PowerKwhPerNm3;
        var pCompressorKw = qCbgNm3Hr * EngineeringConstants.CompressorSpecificEnergyKwhPerNm3;

        // Storage cascade (buffer for 50% of daily production at 250 bar)
        var targetBufferNm3 = qCbgNm3Day * EngineeringConstants.CascadeBufferFraction;
        var waterCapacityLitersReq =
            (targetBufferNm3 *
             EngineeringConstants.StandardPressureBar *
             EngineeringConstants.CascadeCompressibilityZ *
             EngineeringConstants.CascadeActualTempK) /
            (EngineeringConstants.CascadePressureBar * EngineeringConstants.StandardTempK) * 1000.0;

        var numCascades = (int)Math.Ceiling(waterCapacityLitersReq / EngineeringConstants.CascadeCylinderLiters);

        return new UpgradingResult(
            QCbgNm3Day: qCbgNm3Day,
            MCbgKgDay: mCbgKgDay,
            UpgradingPowerKw: pUpgradingKw,
            CompressorPowerKw: pCompressorKw,
            CompressorCapacityNm3Hr: Math.Ceiling(qCbgNm3Hr * EngineeringConstants.CompressorSurgeFactor),
            CascadeWaterCapacityLiters: Math.Ceiling(waterCapacityLitersReq),
            NumCascadeBanks3000L: numCascades);
    }
}