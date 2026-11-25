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
    double ActualOlr,
    double DigestateStorageVolumeM3,
    int NumDigestateTanks,
    double DigestateTankDiameterM,
    double DigestateTankHeightM);

public class DigesterSizingService
{
    private const double DigestateStorageDays = 7.0;
    private const double DigestateTankHeightM = 6.0;
    private const double MaxDigestateTankVolumeM3 = 2000.0;
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

        // V = (pi / 4) * (H/D) * D^3  =>  D = (4 * V / (pi * H/D))^(1/3)
        var diameterM = Math.Pow((4.0 * vGrossPerTank) / (Math.PI * EngineeringConstants.DigesterAspectRatioHd), 1.0 / 3.0);

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


        // Secondary digestate storage tanks
        var qDigestateM3Day = qSlurryM3Day * EngineeringConstants.DigestateFlowFactor;
        var digestateStorageVolumeM3 = Math.Ceiling(qDigestateM3Day * DigestateStorageDays);
        var numDigestateTanks = Math.Max(
            (int)Math.Ceiling(digestateStorageVolumeM3 / MaxDigestateTankVolumeM3), 1);
        var digestateVolumePerTankM3 = digestateStorageVolumeM3 / numDigestateTanks;
        var digestateTankDiameterM = Math.Ceiling(
            Math.Sqrt((4.0 * digestateVolumePerTankM3) / (Math.PI * DigestateTankHeightM))
            / EngineeringConstants.DiameterRoundingStepM) * EngineeringConstants.DiameterRoundingStepM;

        return new DigesterSizingResult(
    NumTanks: numTanks,
    VWorkingPerTankM3: vWorkingActualPerTank,
    VGrossPerTankM3: vGrossActualPerTank,
    DiameterM: diameterM,
    TotalHeightM: totalHeightM,
    LiquidHeightM: liquidHeightM,
    ActualHrtDays: vWorkingActualTotal / qSlurryM3Day,
    ActualOlr: vsTotalKgDay / vWorkingActualTotal,
    DigestateStorageVolumeM3: digestateStorageVolumeM3,
    NumDigestateTanks: numDigestateTanks,
    DigestateTankDiameterM: digestateTankDiameterM,
    DigestateTankHeightM: DigestateTankHeightM);
    }
}