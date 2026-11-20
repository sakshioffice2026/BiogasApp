using BiogasApp.Business.Constants;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Engine;

public record DigesterSizingResult(
    int NumTanks,
    double VWorkingPerTankM3,
    double VGrossPerTankM3,
    double DiameterM,
    double TotalHeightM,
    double LiquidHeightM,
    double ActualHrtDays,
    double ActualOlr);

public class DigesterSizingService
{
    public DigesterSizingResult Calculate(
        double vsTotalKgDay,
        double qSlurryM3Day,
        DigesterRegime regime,
        double maxSingleTankVolM3 = EngineeringConstants.MaxSingleTankVolumeM3)
    {
        if (qSlurryM3Day <= 0)
        {
            throw new ArgumentException("Slurry flow must be greater than zero.", nameof(qSlurryM3Day));
        }

        var isMesophilic = regime == DigesterRegime.Mesophilic;
        var hrtDesign = isMesophilic
            ? EngineeringConstants.HrtMesophilicDays
            : EngineeringConstants.HrtThermophilicDays;
        var olrDesign = isMesophilic
            ? EngineeringConstants.OlrMesophilicKgVsM3Day
            : EngineeringConstants.OlrThermophilicKgVsM3Day;

        var vReqHrt = qSlurryM3Day * hrtDesign;
        var vReqOlr = vsTotalKgDay / olrDesign;
        var vWorkingTotal = Math.Max(vReqHrt, vReqOlr);

        var fHeadspace = EngineeringConstants.HeadspaceFactor;
        var vGrossTotal = vWorkingTotal / (1.0 - fHeadspace);

        var numTanks = (int)Math.Ceiling(vGrossTotal / maxSingleTankVolM3);
        var vGrossPerTank = vGrossTotal / numTanks;

        // V = (pi * D^3) / 8 for H/D = 0.5  =>  D = (8 * V / pi)^(1/3)
        var diameterM = Math.Pow((8.0 * vGrossPerTank) / Math.PI, 1.0 / 3.0);

        // Round diameter up to the nearest 0.5 m for standard manufacturing
        diameterM = Math.Ceiling(diameterM / EngineeringConstants.DiameterRoundingStepM)
                    * EngineeringConstants.DiameterRoundingStepM;

        var totalHeightM = Math.Ceiling(
            (vGrossPerTank / (Math.PI * Math.Pow(diameterM, 2) / 4.0)) * 100.0) / 100.0;
        var liquidHeightM = totalHeightM * (1.0 - fHeadspace);

        var tankAreaM2 = Math.PI * Math.Pow(diameterM, 2) / 4.0;
        var vGrossActualPerTank = tankAreaM2 * totalHeightM;
        var vWorkingActualPerTank = tankAreaM2 * liquidHeightM;
        var vWorkingActualTotal = vWorkingActualPerTank * numTanks;

        return new DigesterSizingResult(
            NumTanks: numTanks,
            VWorkingPerTankM3: vWorkingActualPerTank,
            VGrossPerTankM3: vGrossActualPerTank,
            DiameterM: diameterM,
            TotalHeightM: totalHeightM,
            LiquidHeightM: liquidHeightM,
            ActualHrtDays: vWorkingActualTotal / qSlurryM3Day,
            ActualOlr: vsTotalKgDay / vWorkingActualTotal);
    }
}