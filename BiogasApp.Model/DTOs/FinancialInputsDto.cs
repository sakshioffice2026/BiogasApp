namespace BiogasApp.Model.DTOs;

public class FinancialInputsDto
{
    public double CbgSellingPricePerKg { get; set; }
    public double FomSellingPricePerTon { get; set; }
    public double DiscountRatePercent { get; set; } = 10.0;
    public int ProjectLifeYears { get; set; } = 15;
    public double DebtRatioPercent { get; set; } = 70.0;
    public double InterestRatePercent { get; set; } = 8.5;
}

public class RateCardOverrideDto
{
    public string ItemCode { get; set; } = string.Empty;
    public decimal OverrideUnitRate { get; set; }
}

public class ProposalRequestDto
{
    public Guid SimulationRunId { get; set; }
    public FinancialInputsDto FinancialInputs { get; set; } = new();
    public List<RateCardOverrideDto> CustomRateCardOverrides { get; set; } = new();
}