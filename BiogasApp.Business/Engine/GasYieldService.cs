using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record GasYieldResult(
    double QBiogasNm3Day,
    double QBiogasNm3Hr,
    double QCh4Nm3Day,
    double Ch4Pct,
    double Co2Pct,
    double H2sPpm,
    double MH2sKgDay);

public class GasYieldService
{
    public GasYieldResult Calculate(IReadOnlyList<FeedstockInput> feedstocks)
    {
        if (feedstocks == null || feedstocks.Count == 0)
        {
            throw new ArgumentException("At least one feedstock is required.", nameof(feedstocks));
        }

        double qBiogasTotalNm3Day = 0.0;
        double qCh4TotalNm3Day = 0.0;
        double h2sWeightedPpm = 0.0;

        foreach (var f in feedstocks)
        {
            var vsKg = f.MFeedTpd * 1000.0 * (f.TsPct / 100.0) * (f.VsPctTs / 100.0);
            var degradation = f.DegradationFactor > 0
                ? f.DegradationFactor
                : EngineeringConstants.DefaultDegradationFactor;

            var biogasYield = vsKg * f.Bmp * degradation;
            var methaneFraction = f.MethanePct.HasValue && f.MethanePct.Value > 0
     ? f.MethanePct.Value / 100.0
     : EngineeringConstants.MethaneFraction;
            var ch4Yield = biogasYield * methaneFraction;

            qBiogasTotalNm3Day += biogasYield;
            qCh4TotalNm3Day += ch4Yield;

            double h2sFeedPpm;
            if (f.H2sPpm.HasValue && f.H2sPpm.Value >= 0)
            {
                h2sFeedPpm = f.H2sPpm.Value;
            }
            else if (f.Category == EngineeringConstants.AgroCategoryName)
            {
                h2sFeedPpm = EngineeringConstants.H2sPpmAgro;
            }
            else if (f.Name == EngineeringConstants.ChickenLitterName)
            {
                h2sFeedPpm = EngineeringConstants.H2sPpmChickenLitter;
            }
            else
            {
                h2sFeedPpm = EngineeringConstants.H2sPpmDefault;
            }

            h2sWeightedPpm += h2sFeedPpm * biogasYield;
        }
     
        if (qBiogasTotalNm3Day <= 0)
        {
            throw new InvalidOperationException("Total biogas yield is zero. Check feedstock inputs.");
        }

        h2sWeightedPpm /= qBiogasTotalNm3Day;

        var ch4Pct = (qCh4TotalNm3Day / qBiogasTotalNm3Day) * 100.0;
        var co2Pct = Math.Max(0.0, 100.0 - ch4Pct - EngineeringConstants.MinorGasesPercent);

        var mH2sKgDay = qBiogasTotalNm3Day * (h2sWeightedPpm / 1e6) * EngineeringConstants.H2sKgPerNm3;

        return new GasYieldResult(
            QBiogasNm3Day: qBiogasTotalNm3Day,
            QBiogasNm3Hr: qBiogasTotalNm3Day / 24.0,
            QCh4Nm3Day: qCh4TotalNm3Day,
            Ch4Pct: ch4Pct,
            Co2Pct: co2Pct,
            H2sPpm: h2sWeightedPpm,
            MH2sKgDay: mH2sKgDay);
    }
}