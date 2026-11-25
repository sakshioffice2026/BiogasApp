namespace BiogasApp.Business.Engine;

public record PumpSizing(
    double FlowM3Hr,
    double HeadM,
    double PowerKw);

public record PumpPipingResult(
    PumpSizing FeedPump,
    PumpSizing DigestatePump,
    int SlurryLineDnMm,
    int BiogasLineDnMm,
    int DigestateLineDnMm);

public class PumpPipingService
{
    private const double PumpOperatingHoursPerDay = 20.0;
    private const double PumpCapacityFactor = 1.20;
    private const double FeedPumpHeadM = 25.0;
    private const double DigestatePumpHeadM = 20.0;
    private const double PumpEfficiency = 0.50;
    private const double MotorMarginFactor = 1.15;
    private const double SlurryDensityKgM3 = 1020.0;
    private const double DigestateFlowFactor = 0.92;
    private const double Gravity = 9.81;
    private const double SlurryVelocityMs = 1.5;
    private const double BiogasVelocityMs = 10.0;

    private static readonly int[] StandardDnMm =
        { 25, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500, 600 };

    public PumpPipingResult Calculate(
        double qSlurryM3Day,
        double qBiogasNm3Hr)
    {
        if (qSlurryM3Day <= 0)
        {
            throw new ArgumentException("Slurry flow must be greater than zero.", nameof(qSlurryM3Day));
        }

        if (qBiogasNm3Hr <= 0)
        {
            throw new ArgumentException("Biogas flow must be greater than zero.", nameof(qBiogasNm3Hr));
        }

        var feedFlowM3Hr = qSlurryM3Day / PumpOperatingHoursPerDay * PumpCapacityFactor;
        var digestateFlowM3Hr = qSlurryM3Day * DigestateFlowFactor / PumpOperatingHoursPerDay * PumpCapacityFactor;

        var feedPump = new PumpSizing(
            FlowM3Hr: Math.Ceiling(feedFlowM3Hr),
            HeadM: FeedPumpHeadM,
            PowerKw: PumpPowerKw(feedFlowM3Hr, FeedPumpHeadM));

        var digestatePump = new PumpSizing(
            FlowM3Hr: Math.Ceiling(digestateFlowM3Hr),
            HeadM: DigestatePumpHeadM,
            PowerKw: PumpPowerKw(digestateFlowM3Hr, DigestatePumpHeadM));

        var slurryLineDn = SizeLineDn(feedFlowM3Hr, SlurryVelocityMs);
        var digestateLineDn = SizeLineDn(digestateFlowM3Hr, SlurryVelocityMs);
        var biogasLineDn = SizeLineDn(qBiogasNm3Hr, BiogasVelocityMs);

        return new PumpPipingResult(
            FeedPump: feedPump,
            DigestatePump: digestatePump,
            SlurryLineDnMm: slurryLineDn,
            BiogasLineDnMm: biogasLineDn,
            DigestateLineDnMm: digestateLineDn);
    }

    private static double PumpPowerKw(double flowM3Hr, double headM)
    {
        var flowM3S = flowM3Hr / 3600.0;
        var hydraulicKw = SlurryDensityKgM3 * Gravity * flowM3S * headM / 1000.0;
        return Math.Round(hydraulicKw / PumpEfficiency * MotorMarginFactor, 2);
    }

    private static int SizeLineDn(double flowPerHour, double velocityMs)
    {
        var flowM3S = flowPerHour / 3600.0;
        var requiredDiameterMm = Math.Sqrt(4.0 * flowM3S / (Math.PI * velocityMs)) * 1000.0;

        foreach (var dn in StandardDnMm)
        {
            if (dn >= requiredDiameterMm)
            {
                return dn;
            }
        }

        return (int)Math.Ceiling(requiredDiameterMm);
    }
}