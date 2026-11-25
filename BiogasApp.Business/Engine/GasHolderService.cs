namespace BiogasApp.Business.Engine;

public record GasHolderResult(
    double RequiredBufferM3,
    double DomeHeightM,
    double DomeVolumePerTankM3,
    double TotalDomeVolumeM3,
    double AdditionalHolderVolumeM3,
    int NumExternalHolders,
    double VolumePerExternalHolderM3);

public class GasHolderService
{
    private const double BufferHours = 3.0;
    private const double DomeHeightFactor = 0.15;
    private const double MaxExternalHolderVolumeM3 = 1500.0;

    public GasHolderResult Calculate(
        double qBiogasNm3Day,
        double diameterM,
        int numTanks)
    {
        if (qBiogasNm3Day <= 0)
        {
            throw new ArgumentException("Biogas flow must be greater than zero.", nameof(qBiogasNm3Day));
        }

        if (diameterM <= 0)
        {
            throw new ArgumentException("Diameter must be greater than zero.", nameof(diameterM));
        }

        if (numTanks < 1)
        {
            throw new ArgumentException("Tank count must be at least one.", nameof(numTanks));
        }

        var qBiogasNm3Hr = qBiogasNm3Day / 24.0;
        var requiredBufferM3 = qBiogasNm3Hr * BufferHours;

        var domeHeightM = Math.Round(diameterM * DomeHeightFactor, 1);
        var baseRadiusM = diameterM / 2.0;

        // Spherical cap volume: V = (pi * h / 6) * (3a^2 + h^2)
        var domeVolumePerTankM3 = (Math.PI * domeHeightM / 6.0) *
                                  (3.0 * Math.Pow(baseRadiusM, 2) + Math.Pow(domeHeightM, 2));
        var totalDomeVolumeM3 = domeVolumePerTankM3 * numTanks;

        var additionalVolumeM3 = Math.Max(requiredBufferM3 - totalDomeVolumeM3, 0.0);

        var numExternalHolders = additionalVolumeM3 > 0
            ? (int)Math.Ceiling(additionalVolumeM3 / MaxExternalHolderVolumeM3)
            : 0;

        var volumePerExternalHolderM3 = numExternalHolders > 0
            ? Math.Ceiling(additionalVolumeM3 / numExternalHolders)
            : 0.0;

        return new GasHolderResult(
            RequiredBufferM3: requiredBufferM3,
            DomeHeightM: domeHeightM,
            DomeVolumePerTankM3: domeVolumePerTankM3,
            TotalDomeVolumeM3: totalDomeVolumeM3,
            AdditionalHolderVolumeM3: additionalVolumeM3,
            NumExternalHolders: numExternalHolders,
            VolumePerExternalHolderM3: volumePerExternalHolderM3);
    }
}