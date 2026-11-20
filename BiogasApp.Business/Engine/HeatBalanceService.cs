using BiogasApp.Business.Constants;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Engine;

public record HeatBalanceResult(
    double QFeedHeatingKw,
    double QTransmissionLossKw,
    double QTotalThermalDutyKw,
    double HeatExchangerAreaM2);

public class HeatBalanceService
{
    private const double ThermalBridgeAllowanceFactor = 1.05;
    public static double GetOperatingTempC(DigesterRegime regime) =>
        regime == DigesterRegime.Mesophilic
            ? EngineeringConstants.MesophilicOperatingTempC
            : EngineeringConstants.ThermophilicOperatingTempC;

    public HeatBalanceResult Calculate(
        double qSlurryM3Day,
        double diameterM,
        double totalHeightM,
        double liquidHeightM,
        double tOpC = EngineeringConstants.MesophilicOperatingTempC,
        double tAmbMinC = EngineeringConstants.DefaultAmbientMinTempC)
    {
        var deltaT = tOpC - tAmbMinC;

        // Feed heating
        var mDotSlurryKgS = (qSlurryM3Day * EngineeringConstants.SlurryHeatDensityKgM3) / 86400.0;
        var qFeedKw = mDotSlurryKgS * EngineeringConstants.SlurrySpecificHeatKjPerKgK * deltaT;

        // Surface areas
        var aFloor = Math.PI * Math.Pow(diameterM, 2) / 4.0;
        var aWallLiquid = Math.PI * diameterM * liquidHeightM;
        var aWallExposed = Math.PI * diameterM * Math.Max(totalHeightM - liquidHeightM, 0.0);
        var aRoof = Math.PI * Math.Pow(diameterM, 2) / 4.0 * EngineeringConstants.RoofAreaFactor;

        // Transmission losses
        var qLossW =
            (EngineeringConstants.UFloorWPerM2K * aFloor * deltaT) +
            (EngineeringConstants.UWallWPerM2K * aWallLiquid * deltaT) +
            (EngineeringConstants.UWallWPerM2K * aWallExposed * deltaT) +
            (EngineeringConstants.URoofWPerM2K * aRoof * deltaT);
        var qLossKw = (qLossW * ThermalBridgeAllowanceFactor) / 1000.0;
        

        var qTotalKw = (qFeedKw + qLossKw) * EngineeringConstants.HeatSafetyFactor;

        // External tube-in-tube heat exchanger
        var aHxM2 = (qTotalKw * 1000.0) / (EngineeringConstants.HxUWPerM2K * EngineeringConstants.HxLmtdC);

        return new HeatBalanceResult(
            QFeedHeatingKw: qFeedKw,
            QTransmissionLossKw: qLossKw,
            QTotalThermalDutyKw: qTotalKw,
            HeatExchangerAreaM2: Math.Ceiling(aHxM2 * EngineeringConstants.HxAreaMargin));
    }
}