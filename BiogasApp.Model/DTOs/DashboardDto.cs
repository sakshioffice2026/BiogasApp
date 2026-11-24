using BiogasApp.Database.Enums;

namespace BiogasApp.Model.DTOs;

public class DashboardDto
{
    public Guid SimulationRunId { get; set; }
    public double RawBiogasNm3Day { get; set; }
    public double NetCbgKgDay { get; set; }
    public int DigesterCount { get; set; }
    public double DigesterDiameterM { get; set; }
    public double DigesterHeightM { get; set; }
    public double DigesterVolumeM3 { get; set; }
    public double ParasiticPowerKw { get; set; }
    public double ParasiticHeatKw { get; set; }
    public double BlendedCnRatio { get; set; }
    public string CnGaugeZone { get; set; } = "Green";
    public List<string> Warnings { get; set; } = new();
    public List<TechnologyComparisonDto> TechnologyComparison { get; set; } = new();
}

public class TechnologyComparisonDto
{
    public string Technology { get; set; } = string.Empty;
    public double RecoveryPercent { get; set; }
    public double PurityPercent { get; set; }
    public double UpgradingPowerKw { get; set; }
    public double CompressorPowerKw { get; set; }
    public double NetCbgKgDay { get; set; }
}

public class DashboardOverviewDto
{
    public int TotalProjects { get; set; }
    public int TotalRuns { get; set; }
    public Dictionary<ProjectStatus, int> ProjectStatusCounts { get; set; } = new();

    public DashboardKpiStripDto Kpis { get; set; } = new();
    public List<DashboardProjectRowDto> RecentProjects { get; set; } = new();
    public List<DashboardPendingPidDto> PendingPids { get; set; } = new();
    public DashboardEnquirySummaryDto Enquiries { get; set; } = new();
    public DashboardAssessmentSummaryDto Assessments { get; set; } = new();
    public List<DashboardActivityDto> Activity { get; set; } = new();

    public Guid? FeaturedProjectId { get; set; }
    public Guid? FeaturedRunId { get; set; }
    public string? FeaturedProjectName { get; set; }
}

public class DashboardKpiStripDto
{
    public decimal TotalFeedTpd { get; set; }
    public decimal RawBiogasNm3Day { get; set; }
    public decimal NetCbgKgDay { get; set; }
    public decimal ParasiticPowerKw { get; set; }
    public int ProjectsCounted { get; set; }
}

public class DashboardProjectRowDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? LatestRunId { get; set; }
    public int? LatestRunVersion { get; set; }

    public bool HasLayout { get; set; }
    public bool HasPfd { get; set; }
    public bool HasPid { get; set; }
    public PidApprovalStatus? PidStatus { get; set; }
    public Guid? PidDiagramId { get; set; }
}

public class DashboardPendingPidDto
{
    public Guid PidDiagramId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string DrawingNo { get; set; } = string.Empty;
    public int RevisionNo { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DashboardEnquirySummaryDto
{
    public int Total { get; set; }
    public int New { get; set; }
    public int UnderReview { get; set; }
    public int ProposalSent { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }

    public int ProposalsDraft { get; set; }
    public int ProposalsSent { get; set; }
    public int ProposalsApproved { get; set; }
}

public class DashboardAssessmentSummaryDto
{
    public int Total { get; set; }
    public int Draft { get; set; }
    public int Converted { get; set; }
}

public class DashboardActivityDto
{
    public DateTime At { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? By { get; set; }
    public Guid? ProjectId { get; set; }
}