namespace BiogasApp.Business.Financial;

public record FinancialInputs(
    double CbgSellingPricePerKg,
    double FomSellingPricePerTon,
    double DiscountRatePercent,
    int ProjectLifeYears,
    double DebtRatioPercent,
    double InterestRatePercent);

public record FinancialMetricsResult(
    double CapexTotal,
    double OpexAnnual,
    double AnnualRevenue,
    double Npv,
    double? IrrPercent,
    double? PaybackPeriodYears,
    double? MinDscr,
    IReadOnlyList<CashFlowYear> CashFlows);

public static class FinancialModel
{
    public static FinancialMetricsResult Evaluate(
        FinancialInputs inputs,
        double capexTotal,
        double opexAnnual,
        double netCbgKgDay,
        double fomTpd,
        int loanTenureYears = CashFlowBuilder.DefaultLoanTenureYears)
    {
        var annualRevenue = CashFlowBuilder.CalculateAnnualRevenue(
            netCbgKgDay,
            inputs.CbgSellingPricePerKg,
            fomTpd,
            inputs.FomSellingPricePerTon);

        var cashFlows = CashFlowBuilder.Build(
            capexTotal,
            opexAnnual,
            annualRevenue,
            inputs.ProjectLifeYears,
            inputs.DebtRatioPercent,
            inputs.InterestRatePercent,
            loanTenureYears);

        var projectFlows = cashFlows.Select(c => c.ProjectCashFlow).ToArray();

        return new FinancialMetricsResult(
            CapexTotal: capexTotal,
            OpexAnnual: opexAnnual,
            AnnualRevenue: annualRevenue,
            Npv: Npv(inputs.DiscountRatePercent, projectFlows),
            IrrPercent: Irr(projectFlows),
            PaybackPeriodYears: PaybackYears(projectFlows),
            MinDscr: MinDscr(cashFlows),
            CashFlows: cashFlows);
    }

    public static double Npv(double discountRatePercent, IReadOnlyList<double> flows)
    {
        var r = discountRatePercent / 100.0;
        double total = 0;
        for (var t = 0; t < flows.Count; t++)
        {
            total += flows[t] / Math.Pow(1.0 + r, t);
        }
        return total;
    }

    public static double? Irr(IReadOnlyList<double> flows)
    {
        double F(double r)
        {
            double total = 0;
            for (var t = 0; t < flows.Count; t++)
            {
                total += flows[t] / Math.Pow(1.0 + r, t);
            }
            return total;
        }

        double low = -0.99, high = 10.0;
        var fLow = F(low);
        var fHigh = F(high);

        if (double.IsNaN(fLow) || double.IsNaN(fHigh) || fLow * fHigh > 0)
        {
            return null;
        }

        for (var i = 0; i < 200; i++)
        {
            var mid = (low + high) / 2.0;
            var fMid = F(mid);

            if (Math.Abs(fMid) < 1e-6)
            {
                return mid * 100.0;
            }

            if (fLow * fMid < 0)
            {
                high = mid;
            }
            else
            {
                low = mid;
                fLow = fMid;
            }
        }

        return ((low + high) / 2.0) * 100.0;
    }

    public static double? PaybackYears(IReadOnlyList<double> flows)
    {
        double cumulative = 0;
        for (var t = 0; t < flows.Count; t++)
        {
            var previous = cumulative;
            cumulative += flows[t];

            if (t > 0 && cumulative >= 0 && flows[t] > 0)
            {
                return (t - 1) + (-previous / flows[t]);
            }
        }
        return null;
    }

    public static double? MinDscr(IReadOnlyList<CashFlowYear> cashFlows)
    {
        var ratios = cashFlows
            .Where(c => c.DebtService > 0)
            .Select(c => c.Ebitda / c.DebtService)
            .ToList();

        return ratios.Count == 0 ? null : ratios.Min();
    }
}