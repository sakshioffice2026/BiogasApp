using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record PreTreatmentResult(
    double ShredderCapacityTph,
    double ShredderPowerKw,
    double MixTankVolumeM3,
    double MixerPowerKw,
    double DilutionWaterM3Day,
    double FeedTankVolumeM3,
    double FeedTankDiameterM,
    double FeedTankHeightM,
    double FeedTankMixerPowerKw,
    double MixTankDiameterM,
    double MixTankHeightM);

public class PreTreatmentService
{
    private const double FeedTankBufferDays = 1.0;
    private const double FeedTankHeightM = 5.0;
    private const double MixTankHeightM = 4.0;
    public PreTreatmentResult Calculate(
        double mFeedTotalTpd,
        double mWaterTpd,
        double qSlurryM3Day)
    {
        var shredderCapacityTph =
            mFeedTotalTpd / EngineeringConstants.ShredderOperatingHoursPerDay *
            EngineeringConstants.ShredderCapacityFactor;

        var shredderPowerKw =
            mFeedTotalTpd * EngineeringConstants.ShredderSpecificEnergyKwhPerTon /
            EngineeringConstants.ShredderOperatingHoursPerDay;

        var mixTankVolumeM3 =
            qSlurryM3Day / 24.0 * EngineeringConstants.MixTankResidenceHours;

        var mixerPowerKw =
            mixTankVolumeM3 * EngineeringConstants.MixTankPowerDensityWPerM3 / 1000.0;

        var mixTankDiameterM = Math.Ceiling(
            Math.Sqrt((4.0 * mixTankVolumeM3) / (Math.PI * MixTankHeightM))
            / EngineeringConstants.DiameterRoundingStepM) * EngineeringConstants.DiameterRoundingStepM;

        var dilutionWaterM3Day =
            mWaterTpd / EngineeringConstants.WaterDensityTPerM3;


        // Feed tank (buffer between mix tank and digester)
        var feedTankVolumeM3 = Math.Ceiling(qSlurryM3Day * FeedTankBufferDays);
        var feedTankDiameterM = Math.Ceiling(
            Math.Sqrt((4.0 * feedTankVolumeM3) / (Math.PI * FeedTankHeightM))
            / EngineeringConstants.DiameterRoundingStepM) * EngineeringConstants.DiameterRoundingStepM;
        var feedTankMixerPowerKw =
            feedTankVolumeM3 * EngineeringConstants.MixTankPowerDensityWPerM3 / 1000.0;

        return new PreTreatmentResult(
    ShredderCapacityTph: shredderCapacityTph,
    ShredderPowerKw: shredderPowerKw,
    MixTankVolumeM3: mixTankVolumeM3,
    MixerPowerKw: mixerPowerKw,
    DilutionWaterM3Day: dilutionWaterM3Day,
    FeedTankVolumeM3: feedTankVolumeM3,
    FeedTankDiameterM: feedTankDiameterM,
    FeedTankHeightM: FeedTankHeightM,
    FeedTankMixerPowerKw: feedTankMixerPowerKw,
    MixTankDiameterM: mixTankDiameterM,
    MixTankHeightM: MixTankHeightM);
    }
}
