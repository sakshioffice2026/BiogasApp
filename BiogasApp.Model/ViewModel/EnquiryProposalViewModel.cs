using System.ComponentModel.DataAnnotations;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class ProposalFeedstockLineViewModel
{
    [Required]
    [Display(Name = "Feedstock")]
    public Guid FeedstockId { get; set; }

    [Range(0.1, 100000)]
    [Display(Name = "Quantity (TPD)")]
    public decimal DailyTonnageTpd { get; set; }

    [Range(1, 365)]
    [Display(Name = "Availability (days/year)")]
    public int SeasonalAvailabilityDays { get; set; } = 365;
}

public class FeedstockOptionViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class EnquiryProposalFormViewModel
{
    public Guid EnquiryId { get; set; }
    public string EnquiryNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string FeedstockRequested { get; set; } = string.Empty;
    public decimal QuantityRequestedTpd { get; set; }
    public string EndUseRequested { get; set; } = string.Empty;
    public Guid? AssessmentId { get; set; }
    public Guid? ProjectId { get; set; }
    public int NextVersion { get; set; } = 1;

    [Display(Name = "Regulatory Code")]
    public RegulatoryStandard RegulatoryStandard { get; set; } = RegulatoryStandard.PesoIndia;

    [Range(-50, 60)]
    [Display(Name = "Ambient Temp Min (C)")]
    public decimal AmbientTempMinC { get; set; } = 10m;

    [Range(-50, 60)]
    [Display(Name = "Ambient Temp Max (C)")]
    public decimal AmbientTempMaxC { get; set; } = 40m;

    [Display(Name = "Digester Regime")]
    public DigesterRegime DigesterRegime { get; set; } = DigesterRegime.Mesophilic;

    [Display(Name = "Upgrading Technology")]
    public UpgradingTech UpgradingTech { get; set; } = UpgradingTech.Psa;

    public List<ProposalFeedstockLineViewModel> FeedstockLines { get; set; } = new();

    public FinancialInputsDto FinancialInputs { get; set; } = new();

    [StringLength(2000)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    public List<FeedstockOptionViewModel> AvailableFeedstocks { get; set; } = new();
}

public class EnquiryProposalListItemViewModel
{
    public Guid Id { get; set; }
    public string ProposalNo { get; set; } = string.Empty;
    public int Version { get; set; }
    public ProposalStatus Status { get; set; }
    public decimal CapexTotal { get; set; }
    public decimal? IrrPercent { get; set; }
    public decimal? PaybackPeriodYears { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class EnquiryProposalDetailsViewModel
{
    public Guid Id { get; set; }
    public Guid EnquiryId { get; set; }
    public string EnquiryNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid SimulationRunId { get; set; }

    public string ProposalNo { get; set; } = string.Empty;
    public int Version { get; set; }
    public ProposalStatus Status { get; set; }

    public decimal CapexTotal { get; set; }
    public decimal OpexAnnual { get; set; }
    public decimal AnnualRevenue { get; set; }
    public decimal Npv { get; set; }
    public decimal? IrrPercent { get; set; }
    public decimal? PaybackPeriodYears { get; set; }

    public string? Remarks { get; set; }
    public bool HasPdf { get; set; }
    public bool HasBom { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    public List<EnquiryProposalListItemViewModel> Versions { get; set; } = new();
}

public class EnquiryProposalStatusUpdateViewModel
{
    public Guid Id { get; set; }
    public ProposalStatus Status { get; set; }

    [StringLength(2000)]
    public string? Remarks { get; set; }
}
