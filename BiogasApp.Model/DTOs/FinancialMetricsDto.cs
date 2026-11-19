namespace BiogasApp.Model.DTOs;

public class FinancialMetricsDto
{
    public double CapexTotal { get; set; }
    public double OpexAnnual { get; set; }
    public double AnnualRevenue { get; set; }
    public double Npv { get; set; }
    public double? IrrPercent { get; set; }
    public double? PaybackPeriodYears { get; set; }
    public double? MinDscr { get; set; }
}

public class ProposalResponseDto
{
    public Guid ProposalId { get; set; }
    public FinancialMetricsDto FinancialMetrics { get; set; } = new();
    public string? ExcelBomUrl { get; set; }
    public string? ProposalPdfUrl { get; set; }
}