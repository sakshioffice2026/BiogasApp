using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record ShellCourse(int CourseIndex, double CalculatedMm, double SpecifiedMm);

public record MechanicalSizingResult(
    double TotalMixingPowerKw,
    int AgitatorCount,
    double PowerPerAgitatorKw,
    int NumShellCourses,
    IReadOnlyList<ShellCourse> ShellCourses);

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

        // API 650 shell plate thickness
        var courseHeight = EngineeringConstants.ShellCourseHeightM;
        var numCourses = (int)Math.Ceiling(totalHeightM / courseHeight);
        var courses = new List<ShellCourse>();

        for (var i = 0; i < numCourses; i++)
        {
            var hLevel = totalHeightM - (i * courseHeight);

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

        return new MechanicalSizingResult(
            TotalMixingPowerKw: pMixTotalKw,
            AgitatorCount: numAgitators,
            PowerPerAgitatorKw: powerPerAgitatorKw,
            NumShellCourses: numCourses,
            ShellCourses: courses);
    }
}