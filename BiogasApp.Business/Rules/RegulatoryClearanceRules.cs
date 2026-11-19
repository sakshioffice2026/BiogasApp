using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Rules;

public record ClearanceCheck(string Rule, double ActualM, double RequiredM)
{
    public bool Passed => ActualM >= RequiredM;
}

public class RegulatoryClearanceException : Exception
{
    public const string ErrorCode = "REGULATORY_CLEARANCE_VIOLATION";

    public IReadOnlyList<ClearanceCheck> Violations { get; }

    public RegulatoryClearanceException(IReadOnlyList<ClearanceCheck> violations)
        : base(BuildMessage(violations))
    {
        Violations = violations;
    }

    private static string BuildMessage(IReadOnlyList<ClearanceCheck> violations)
    {
        var details = string.Join("; ", violations.Select(v =>
            $"{v.Rule}: actual {v.ActualM:F2} m, required >= {v.RequiredM:F2} m"));
        return $"{ErrorCode}: {details}";
    }
}

public static class RegulatoryClearanceRules
{
    public static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static ClearanceCheck CheckFlareToGasStorage(double actualM) =>
        new("FLARE_STACK_TO_GAS_STORAGE", actualM, EngineeringConstants.FlareToGasStorageMinM);

    public static ClearanceCheck CheckCascadeToBoundary(double actualM) =>
        new("CBG_CASCADE_TO_SITE_BOUNDARY", actualM, EngineeringConstants.CascadeToBoundaryMinM);

    public static ClearanceCheck CheckDigesterWallToWall(double actualM) =>
        new("DIGESTER_WALL_TO_WALL", actualM, EngineeringConstants.DigesterWallToWallMinM);

    public static ClearanceCheck CheckOfficeToCascade(double actualM) =>
        new("OFFICE_CONTROL_TO_CASCADE", actualM, EngineeringConstants.OfficeControlMinM);

    public static bool IsPlotAreaValid(double plotXM, double plotYM) =>
        plotXM * plotYM >= EngineeringConstants.MinPlotAreaM2;

    public static void EnsureCompliant(IEnumerable<ClearanceCheck> checks)
    {
        var violations = checks.Where(c => !c.Passed).ToList();
        if (violations.Count > 0)
        {
            throw new RegulatoryClearanceException(violations);
        }
    }
}