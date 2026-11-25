namespace BiogasApp.Business.Engine;

public record GasTreatmentResult(
    double H2sRemovedKgDay,
    double DesulphurisationAirNm3Hr,
    double PolishingMediaKg,
    int NumPolishingVessels,
    double GasChillerDutyKw,
    double BoosterCapacityNm3Hr,
    double BoosterPowerKw,
    double FlareCapacityNm3Hr);

public class GasTreatmentService
{
    private const double BiologicalAirDosingFraction = 0.04;
    private const double H2sLoadingKgPerKgMedia = 0.15;
    private const double MediaChangeoutDays = 90.0;
    private const double MediaPerVesselKg = 2000.0;
    private const double ChillerSpecificDutyKwPerNm3Hr = 0.025;
    private const double BoosterCapacityFactor = 1.20;
    private const double BoosterSpecificEnergyKwhPerNm3 = 0.03;
    private const double FlareCapacityFactor = 1.10;
    private const double PolishingRemovalFraction = 0.10;

    public GasTreatmentResult Calculate(
        double qBiogasNm3Day,
        double mH2sKgDay)
    {
        if (qBiogasNm3Day <= 0)
        {
            throw new ArgumentException("Biogas flow must be greater than zero.", nameof(qBiogasNm3Day));
        }

        if (mH2sKgDay < 0)
        {
            throw new ArgumentException("H2S mass must not be negative.", nameof(mH2sKgDay));
        }

        var qBiogasNm3Hr = qBiogasNm3Day / 24.0;

        var desulphurisationAirNm3Hr = qBiogasNm3Hr * BiologicalAirDosingFraction;

        var h2sToPolishKgDay = mH2sKgDay * PolishingRemovalFraction;
        var polishingMediaKg = Math.Ceiling(
            (h2sToPolishKgDay * MediaChangeoutDays) / H2sLoadingKgPerKgMedia);
        var numPolishingVessels = Math.Max(
            (int)Math.Ceiling(polishingMediaKg / MediaPerVesselKg), 1);

        var gasChillerDutyKw = qBiogasNm3Hr * ChillerSpecificDutyKwPerNm3Hr;

        var boosterCapacityNm3Hr = Math.Ceiling(qBiogasNm3Hr * BoosterCapacityFactor);
        var boosterPowerKw = qBiogasNm3Hr * BoosterSpecificEnergyKwhPerNm3;

        var flareCapacityNm3Hr = Math.Ceiling(qBiogasNm3Hr * FlareCapacityFactor);

        return new GasTreatmentResult(
            H2sRemovedKgDay: mH2sKgDay,
            DesulphurisationAirNm3Hr: desulphurisationAirNm3Hr,
            PolishingMediaKg: polishingMediaKg,
            NumPolishingVessels: numPolishingVessels,
            GasChillerDutyKw: gasChillerDutyKw,
            BoosterCapacityNm3Hr: boosterCapacityNm3Hr,
            BoosterPowerKw: boosterPowerKw,
            FlareCapacityNm3Hr: flareCapacityNm3Hr);
    }
}