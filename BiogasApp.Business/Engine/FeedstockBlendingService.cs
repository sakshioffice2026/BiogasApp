using BiogasApp.Business.Constants;
using BiogasApp.Business.Rules;

namespace BiogasApp.Business.Engine;

public record FeedstockInput(
    string Name,
    string Category,
    double MFeedTpd,
    double TsPct,
    double VsPctTs,
    double CnRatio,
    double Bmp,
    double DegradationFactor);

public record BlendingResult(
    double MFeedTotalTpd,
    double TsTotalKgDay,
    double VsTotalKgDay,
    double BlendedTsPct,
    double BlendedVsPctTs,
    double BlendedCnRatio,
    double MWaterTpd,
    double QSlurryM3Day,
    IReadOnlyList<string> Warnings);

public class FeedstockBlendingService
{
    public BlendingResult Calculate(
        IReadOnlyList<FeedstockInput> feedstocks,
        double targetSlurryTsPercent = EngineeringConstants.DefaultTargetSlurryTsPercent)
    {
        if (feedstocks == null || feedstocks.Count == 0)
        {
            throw new ArgumentException("At least one feedstock is required.", nameof(feedstocks));
        }

        var mFeedTotalTpd = feedstocks.Sum(f => f.MFeedTpd);
        if (mFeedTotalTpd <= 0)
        {
            throw new ArgumentException("Total feed tonnage must be greater than zero.", nameof(feedstocks));
        }

        var tsTotalTpd = feedstocks.Sum(f => f.MFeedTpd * (f.TsPct / 100.0));
        var vsTotalTpd = feedstocks.Sum(f => f.MFeedTpd * (f.TsPct / 100.0) * (f.VsPctTs / 100.0));

        var blendedTsPct = (tsTotalTpd / mFeedTotalTpd) * 100.0;
        var blendedVsPctTs = tsTotalTpd > 0 ? (vsTotalTpd / tsTotalTpd) * 100.0 : 0.0;

        var totalCarbon = feedstocks.Sum(f =>
            f.MFeedTpd * (f.TsPct / 100.0) * (f.CnRatio / (f.CnRatio + 1.0)));
        var totalNitrogen = feedstocks.Sum(f =>
            f.MFeedTpd * (f.TsPct / 100.0) * (1.0 / (f.CnRatio + 1.0)));
        var blendedCnRatio = totalNitrogen > 0 ? totalCarbon / totalNitrogen : 0.0;

        var mSlurryTargetTpd = tsTotalTpd / (targetSlurryTsPercent / 100.0);
        double mWaterTpd;
        if (mSlurryTargetTpd > mFeedTotalTpd)
        {
            mWaterTpd = mSlurryTargetTpd - mFeedTotalTpd;
        }
        else
        {
            mWaterTpd = 0.0;
            mSlurryTargetTpd = mFeedTotalTpd;
        }

        var qSlurryM3Day = mSlurryTargetTpd / EngineeringConstants.SlurryDensityTPerM3;
        var warnings = FeedstockWarningRules.Evaluate(blendedCnRatio);

        return new BlendingResult(
            MFeedTotalTpd: mFeedTotalTpd,
            TsTotalKgDay: tsTotalTpd * 1000.0,
            VsTotalKgDay: vsTotalTpd * 1000.0,
            BlendedTsPct: blendedTsPct,
            BlendedVsPctTs: blendedVsPctTs,
            BlendedCnRatio: blendedCnRatio,
            MWaterTpd: mWaterTpd,
            QSlurryM3Day: qSlurryM3Day,
            Warnings: warnings);
    }
}