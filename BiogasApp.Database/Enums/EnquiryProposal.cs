using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class EnquiryProposal
{
    public Guid Id { get; set; }
    public Guid EnquiryId { get; set; }
    public string ProposalNo { get; set; } = string.Empty;
    public int Version { get; set; } = 1;

    public Guid ProjectId { get; set; }
    public Guid SimulationRunId { get; set; }

    public Guid? AssessmentId { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;

    public decimal CapexTotal { get; set; }
    public decimal OpexAnnual { get; set; }
    public decimal AnnualRevenue { get; set; }
    public decimal Npv { get; set; }
    public decimal? IrrPercent { get; set; }
    public decimal? PaybackPeriodYears { get; set; }

    public string? ProposalPdfUrl { get; set; }
    public string? ExcelBomUrl { get; set; }

    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    public Enquiry? Enquiry { get; set; }
    public Project? Project { get; set; }
    public SimulationRun? SimulationRun { get; set; }
}
