namespace BiogasApp.Business.Engine;

public record UtilitiesResult(
    double ProcessWaterTankM3,
    double FireWaterTankM3,
    double HotWaterGeneratorKw,
    double MccConnectedLoadKw,
    double TransformerKva,
    double DgSetKva);

public class UtilitiesService
{
    private const double ProcessWaterStorageDays = 1.0;
    private const double WashDownAllowanceFraction = 0.20;
    private const double WaterTankMarginFactor = 1.10;
    private const double FireWaterRateM3Hr = 150.0;
    private const double FireWaterDurationHours = 2.0;
    private const double HotWaterGeneratorEfficiency = 0.85;
    private const double HotWaterGeneratorMarginFactor = 1.10;
    private const double MccDesignMarginFactor = 1.25;
    private const double PowerFactor = 0.85;
    private const double TransformerMarginFactor = 1.25;
    private const double DgCoverageFraction = 0.80;

    private static readonly double[] StandardTransformerKva =
        { 100, 160, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500 };

    private static readonly double[] StandardDgKva =
        { 62.5, 100, 125, 160, 200, 250, 320, 380, 500, 625, 750, 1010, 1250 };

    public UtilitiesResult Calculate(
        double dilutionWaterM3Day,
        double parasiticPowerKw,
        double parasiticHeatKw)
    {
        if (dilutionWaterM3Day < 0)
        {
            throw new ArgumentException("Dilution water must not be negative.", nameof(dilutionWaterM3Day));
        }

        if (parasiticPowerKw <= 0)
        {
            throw new ArgumentException("Parasitic power must be greater than zero.", nameof(parasiticPowerKw));
        }

        if (parasiticHeatKw < 0)
        {
            throw new ArgumentException("Parasitic heat must not be negative.", nameof(parasiticHeatKw));
        }

        var processWaterTankM3 = Math.Ceiling(
            dilutionWaterM3Day * ProcessWaterStorageDays *
            (1.0 + WashDownAllowanceFraction) * WaterTankMarginFactor);

        var fireWaterTankM3 = Math.Ceiling(FireWaterRateM3Hr * FireWaterDurationHours);

        var hotWaterGeneratorKw = Math.Ceiling(
            parasiticHeatKw / HotWaterGeneratorEfficiency * HotWaterGeneratorMarginFactor);

        var mccConnectedLoadKw = Math.Ceiling(parasiticPowerKw * MccDesignMarginFactor);

        var transformerRequiredKva = mccConnectedLoadKw / PowerFactor * TransformerMarginFactor;
        var transformerKva = PickStandardSize(StandardTransformerKva, transformerRequiredKva);

        var dgRequiredKva = transformerKva * DgCoverageFraction;
        var dgSetKva = PickStandardSize(StandardDgKva, dgRequiredKva);

        return new UtilitiesResult(
            ProcessWaterTankM3: processWaterTankM3,
            FireWaterTankM3: fireWaterTankM3,
            HotWaterGeneratorKw: hotWaterGeneratorKw,
            MccConnectedLoadKw: mccConnectedLoadKw,
            TransformerKva: transformerKva,
            DgSetKva: dgSetKva);
    }

    private static double PickStandardSize(double[] sizes, double required)
    {
        foreach (var size in sizes)
        {
            if (size >= required)
            {
                return size;
            }
        }

        return Math.Ceiling(required);
    }
}