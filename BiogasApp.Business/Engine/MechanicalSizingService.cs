using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record ShellCourse(int CourseIndex, double CalculatedMm, double SpecifiedMm);

public record MechanicalSizingResult(
    double TotalMixingPowerKw,
    int AgitatorCount,
    double PowerPerAgitatorKw,
    int NumShellCourses,
    IReadOnlyList<ShellCourse> ShellCourses,
    double WindBaseShearKn,
    double WindOverturningMomentKnm,
    double SeismicBaseShearKn,
    double SeismicOverturningMomentKnm,
    double RoofDesignLoadKpa,
    double RoofThicknessMm);

public class MechanicalSizingService
{
    public MechanicalSizingResult Calculate(
        double diameterM,
        double totalHeightM,
        double liquidHeightM,
        double vWorkingM3,
        double tsPercent)
    {
        // Mechanical mixing power density
        var pSpec = tsPercent > EngineeringConstants.MixingTsThresholdPercent
            ? EngineeringConstants.MixingPowerDensityHighTsWPerM3
            : EngineeringConstants.MixingPowerDensityLowTsWPerM3;
        var pMixTotalKw = (vWorkingM3 * pSpec) / 1000.0;

        int numAgitators;
        if (diameterM <= EngineeringConstants.AgitatorDiameterLimit2M)
        {
            numAgitators = 2;
        }
        else if (diameterM <= EngineeringConstants.AgitatorDiameterLimit3M)
        {
            numAgitators = 3;
        }
        else
        {
            numAgitators = 4;
        }

        var powerPerAgitatorKw = Math.Ceiling(pMixTotalKw / numAgitators);

        // Shell plate thickness - API 650 one-foot method, followed by wind, seismic and roof checks
        var courseHeight = EngineeringConstants.ShellCourseHeightM;
        var numCourses = (int)Math.Ceiling(totalHeightM / courseHeight);
        var courses = new List<ShellCourse>();

        for (var i = 0; i < numCourses; i++)
        {
            var hLevel = Math.Max(liquidHeightM - (i * courseHeight) - 0.3, 0.0);

            var tCalc =
                (4.9 * diameterM * hLevel * EngineeringConstants.ShellSlurrySpecificGravity) /
                (EngineeringConstants.ShellAllowableStressMpa * EngineeringConstants.ShellJointEfficiency)
                + EngineeringConstants.ShellCorrosionAllowanceMm;

            double tFinal = EngineeringConstants.CommercialThicknessesMm
                .Where(t => t >= tCalc)
                .Select(t => (double)t)
                .DefaultIfEmpty(Math.Ceiling(tCalc))
                .First();

            courses.Add(new ShellCourse(
                CourseIndex: i + 1,
                CalculatedMm: Math.Round(tCalc, 2),
                SpecifiedMm: Math.Max(tFinal, EngineeringConstants.ShellMinThicknessMm)));
        }

        // Wind load on projected area
        var windPressurePa = 0.5 * EngineeringConstants.AirDensityKgPerM3 *
                             Math.Pow(EngineeringConstants.DesignWindSpeedMs, 2) *
                             EngineeringConstants.WindPressureCoefficient;
        var windBaseShearKn = windPressurePa * diameterM * totalHeightM / 1000.0;
        var windMomentKnm = windBaseShearKn * totalHeightM / 2.0;

        // Seismic base shear (seismic coefficient method)
        var avgShellThicknessM = courses.Average(c => c.SpecifiedMm) / 1000.0;
        var shellWeightKn = Math.PI * diameterM * totalHeightM * avgShellThicknessM *
                            EngineeringConstants.SteelDensityKgPerM3 * EngineeringConstants.GravityMs2 / 1000.0;
        var liquidWeightKn = Math.PI * Math.Pow(diameterM, 2) / 4.0 * liquidHeightM *
                             EngineeringConstants.SlurryHeatDensityKgM3 * EngineeringConstants.GravityMs2 / 1000.0;
        var seismicCoefficient = (EngineeringConstants.SeismicZoneFactor / 2.0) *
                                 (EngineeringConstants.SeismicImportanceFactor /
                                  EngineeringConstants.SeismicResponseReductionFactor) * 2.5;
        var seismicBaseShearKn = seismicCoefficient * (shellWeightKn + liquidWeightKn);
        var seismicMomentKnm = seismicBaseShearKn * liquidHeightM / 2.0;

        // Fixed roof design load and plate thickness
        var roofSelfWeightKpa = EngineeringConstants.RoofMinThicknessMm / 1000.0 *
                                EngineeringConstants.SteelDensityKgPerM3 * EngineeringConstants.GravityMs2 / 1000.0;
        var roofDesignLoadKpa = EngineeringConstants.RoofLiveLoadKpa + roofSelfWeightKpa;
        var roofRequiredMm = EngineeringConstants.RoofMinThicknessMm + EngineeringConstants.ShellCorrosionAllowanceMm;
        var roofThicknessMm = EngineeringConstants.CommercialThicknessesMm
            .Where(t => t >= roofRequiredMm)
            .Select(t => (double)t)
            .DefaultIfEmpty(Math.Ceiling(roofRequiredMm))
            .First();

        return new MechanicalSizingResult(
    TotalMixingPowerKw: pMixTotalKw,
    AgitatorCount: numAgitators,
    PowerPerAgitatorKw: powerPerAgitatorKw,
    NumShellCourses: numCourses,
    ShellCourses: courses,
    WindBaseShearKn: Math.Round(windBaseShearKn, 1),
    WindOverturningMomentKnm: Math.Round(windMomentKnm, 1),
    SeismicBaseShearKn: Math.Round(seismicBaseShearKn, 1),
    SeismicOverturningMomentKnm: Math.Round(seismicMomentKnm, 1),
    RoofDesignLoadKpa: Math.Round(roofDesignLoadKpa, 2),
    RoofThicknessMm: roofThicknessMm);
    }
}