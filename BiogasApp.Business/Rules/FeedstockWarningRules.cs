using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Rules;

public static class FeedstockWarningRules
{
    public const string CnImbalanceCode = "CRITICAL_C_N_IMBALANCE";
    public const string FanToxicityCode = "HIGH_FAN_TOXICITY_RISK";

    public static List<string> Evaluate(double blendedCnRatio)
    {
        var warnings = new List<string>();

        if (blendedCnRatio < EngineeringConstants.CnCriticalMin ||
            blendedCnRatio > EngineeringConstants.CnCriticalMax)
        {
            warnings.Add(
                $"{CnImbalanceCode}: Blended C:N ratio is {blendedCnRatio:F2}. " +
                $"Optimal range is {EngineeringConstants.CnOptimalMin:F0}:1 - {EngineeringConstants.CnOptimalMax:F0}:1.");
        }

        if (blendedCnRatio < EngineeringConstants.FanToxicityCnThreshold)
        {
            warnings.Add($"{FanToxicityCode}: Free Ammonia Nitrogen inhibition probable.");
        }

        return warnings;
    }

    public static string GetCnGaugeZone(double blendedCnRatio)
    {
        if (blendedCnRatio >= EngineeringConstants.CnOptimalMin &&
            blendedCnRatio <= EngineeringConstants.CnOptimalMax)
        {
            return "Green";
        }

        if (blendedCnRatio >= EngineeringConstants.CnCriticalMin &&
            blendedCnRatio <= EngineeringConstants.CnCriticalMax)
        {
            return "Yellow";
        }

        return "Red";
    }
}