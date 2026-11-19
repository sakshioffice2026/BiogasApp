using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class ProposalViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }

    public FinancialInputsDto FinancialInputs { get; set; } = new();
    public List<RateCardOverrideDto> RateCardOverrides { get; set; } = new();

    public FinancialMetricsDto? Metrics { get; set; }
    public Guid? ProposalId { get; set; }
    public string? ExcelBomUrl { get; set; }
    public string? ProposalPdfUrl { get; set; }
}