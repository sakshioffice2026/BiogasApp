using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record PreTreatmentResult(
    double ShredderCapacityTph,
    double ShredderPowerKw,
    double MixTankVolumeM3,
    double MixerPowerKw,
    double DilutionWaterM3Day);

public class PreTreatmentService
{
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

        var dilutionWaterM3Day =
            mWaterTpd / EngineeringConstants.WaterDensityTPerM3;

        return new PreTreatmentResult(
            ShredderCapacityTph: shredderCapacityTph,
            ShredderPowerKw: shredderPowerKw,
            MixTankVolumeM3: mixTankVolumeM3,
            MixerPowerKw: mixerPowerKw,
            DilutionWaterM3Day: dilutionWaterM3Day);
    }
}
