namespace BiogasApp.Business.Financial;

public record CashFlowYear(
    int Year,
    double Revenue,
    double Opex,
    double Ebitda,
    double InterestPaid,
    double PrincipalPaid,
    double DebtService,
    double ProjectCashFlow,
    double EquityCashFlow = 0.0);

public static class CashFlowBuilder
{
    public const int DefaultOperatingDays = (int)BiogasApp.Business.Constants.EngineeringConstants.PlantOperatingDaysPerYear;
    public const int DefaultLoanTenureYears = 10;

    public static double CalculateAnnualRevenue(
        double netCbgKgDay,
        double cbgSellingPricePerKg,
        double fomTpd,
        double fomSellingPricePerTon,
        int operatingDays = DefaultOperatingDays)
    {
        var cbgRevenue = netCbgKgDay * operatingDays * cbgSellingPricePerKg;
        var fomRevenue = fomTpd * operatingDays * fomSellingPricePerTon;
        return cbgRevenue + fomRevenue;
    }

    public static double AnnuityPayment(double principal, double annualRatePercent, int tenureYears)
    {
        if (principal <= 0 || tenureYears <= 0)
        {
            return 0.0;
        }

        var r = annualRatePercent / 100.0;
        if (r == 0)
        {
            return principal / tenureYears;
        }

        var factor = Math.Pow(1.0 + r, tenureYears);
        return principal * r * factor / (factor - 1.0);
    }

    public static IReadOnlyList<CashFlowYear> Build(
        double capexTotal,
        double annualOpex,
        double annualRevenue,
        int projectLifeYears,
        double debtRatioPercent,
        double interestRatePercent,
        int loanTenureYears = DefaultLoanTenureYears)
    {
        var years = new List<CashFlowYear>();

        var debt = capexTotal * (debtRatioPercent / 100.0);
        var tenure = Math.Min(loanTenureYears, projectLifeYears);
        var annuity = AnnuityPayment(debt, interestRatePercent, tenure);
        var r = interestRatePercent / 100.0;
        var balance = debt;

        // Year 0: capital outlay
        years.Add(new CashFlowYear(
            Year: 0,
            Revenue: 0,
            Opex: 0,
            Ebitda: 0,
            InterestPaid: 0,
            PrincipalPaid: 0,
            DebtService: 0,
            ProjectCashFlow: -capexTotal,
            EquityCashFlow: -(capexTotal - debt)));

        for (var year = 1; year <= projectLifeYears; year++)
        {
            double interest = 0, principal = 0, debtService = 0;

            if (year <= tenure && balance > 0)
            {
                interest = balance * r;
                principal = Math.Min(annuity - interest, balance);
                debtService = interest + principal;
                balance -= principal;
            }

            var ebitda = annualRevenue - annualOpex;

            years.Add(new CashFlowYear(
                Year: year,
                Revenue: annualRevenue,
                Opex: annualOpex,
                Ebitda: ebitda,
                InterestPaid: interest,
                PrincipalPaid: principal,
                DebtService: debtService,
                ProjectCashFlow: ebitda,
                EquityCashFlow: ebitda - debtService));
        }

        return years;
    }
}