using System.Globalization;
using BiogasApp.Database;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Builds the KPI dashboard from a stored run.
/// TechnologyComparison is left empty here: it needs the upgrading specs that live in the
/// Business project, so the caller (SimulationController / a Business service) fills it.
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    /// <summary>EquipmentType value of the digester row written when a simulation is saved.</summary>
    public const string DigesterEquipmentType = "PRIMARY_DIGESTER_CSTR";

    private readonly ApplicationDbContext _db;
    private readonly ISimulationRepository _simulations;

    public DashboardRepository(ApplicationDbContext db, ISimulationRepository simulations)
    {
        _db = db;
        _simulations = simulations;
    }

    public async Task<DashboardDto?> GetDashboardAsync(Guid simulationRunId, CancellationToken cancellationToken = default)
    {
        var run = await _simulations.GetRunAsync(simulationRunId, cancellationToken);
        if (run is null) return null;

        var result = await _simulations.GetResultDtoAsync(simulationRunId, cancellationToken);
        if (result is null) return null;

        var digester = result.EquipmentSizing.FirstOrDefault(e => e.EquipmentType == DigesterEquipmentType);

        var cn = (double)run.BlendedCnRatio;

        return new DashboardDto
        {
            SimulationRunId = run.Id,
            RawBiogasNm3Day = (double)run.RawBiogasNm3Day,
            NetCbgKgDay = (double)run.NetCbgKgDay,
            DigesterCount = digester?.Quantity ?? 0,
            DigesterDiameterM = ReadNumber(digester?.GeometryParams, "diameterM") ?? 0,
            DigesterHeightM = ReadNumber(digester?.GeometryParams, "totalHeightM") ?? 0,
            DigesterVolumeM3 = ReadNumber(digester?.ProcessDuty, "workingVolumeM3") ?? 0, // per tank
            ParasiticPowerKw = (double)run.ParasiticPowerKw,
            ParasiticHeatKw = (double)run.ParasiticHeatKw,
            BlendedCnRatio = cn,
            CnGaugeZone = GetCnZone(cn),
            Warnings = result.Warnings,
            TechnologyComparison = new List<TechnologyComparisonDto>()
        };
    }

    public async Task<DashboardDto?> GetLatestDashboardAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var latest = await _simulations.GetLatestRunAsync(projectId, cancellationToken);
        return latest is null ? null : await GetDashboardAsync(latest.Id, cancellationToken);
    }

    /// <summary>Every status is present in the result, with 0 when no project has it.</summary>
    public async Task<Dictionary<ProjectStatus, int>> GetProjectStatusCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await _db.Projects
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = Enum.GetValues<ProjectStatus>().ToDictionary(s => s, _ => 0);

        foreach (var item in counts)
            result[item.Status] = item.Count;

        return result;
    }

    public Task<int> GetTotalRunCountAsync(CancellationToken cancellationToken = default) =>
        _db.SimulationRuns.CountAsync(cancellationToken);

    public async Task<DashboardOverviewDto> GetOverviewAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var dto = new DashboardOverviewDto();

        dto.ProjectStatusCounts = await GetProjectStatusCountsAsync(cancellationToken);
        dto.TotalProjects = dto.ProjectStatusCounts.Values.Sum();
        dto.TotalRuns = await GetTotalRunCountAsync(cancellationToken);

        var projects = await _db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.LocationCountry, p.Status, p.CreatedAt })
            .ToListAsync(cancellationToken);
        var projectName = projects.ToDictionary(p => p.Id, p => p.Name);
        string? NameOf(Guid id) => projectName.TryGetValue(id, out var n) ? n : null;

        var runs = await _db.SimulationRuns.AsNoTracking()
            .Select(r => new { r.Id, r.ProjectId, r.RunVersion, r.TotalFeedTpd, r.RawBiogasNm3Day, r.NetCbgKgDay, r.ParasiticPowerKw, r.CreatedAt })
            .ToListAsync(cancellationToken);
        var runProject = runs.ToDictionary(r => r.Id, r => r.ProjectId);
        var latestRuns = runs.GroupBy(r => r.ProjectId)
            .Select(g => g.OrderByDescending(r => r.RunVersion).First())
            .ToDictionary(r => r.ProjectId);

        dto.Kpis = new DashboardKpiStripDto
        {
            TotalFeedTpd = latestRuns.Values.Sum(r => r.TotalFeedTpd),
            RawBiogasNm3Day = latestRuns.Values.Sum(r => r.RawBiogasNm3Day),
            NetCbgKgDay = latestRuns.Values.Sum(r => r.NetCbgKgDay),
            ParasiticPowerKw = latestRuns.Values.Sum(r => r.ParasiticPowerKw),
            ProjectsCounted = latestRuns.Count
        };

        Guid? featuredId = projectId
            ?? latestRuns.Values.OrderByDescending(r => r.CreatedAt).Select(r => (Guid?)r.ProjectId).FirstOrDefault();
        if (featuredId.HasValue)
        {
            dto.FeaturedProjectId = featuredId;
            dto.FeaturedProjectName = NameOf(featuredId.Value);
            dto.FeaturedRunId = latestRuns.TryGetValue(featuredId.Value, out var fr) ? fr.Id : (Guid?)null;
        }

        var recent = projects.OrderByDescending(p => p.CreatedAt).Take(5).ToList();
        var recentIds = recent.Select(p => p.Id).ToList();

        var layoutProjects = (await _db.SiteLayouts.AsNoTracking()
            .Where(l => recentIds.Contains(l.ProjectId))
            .Select(l => l.ProjectId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var pfdRunIds = await _db.GeneratedArtifacts.AsNoTracking()
            .Where(a => a.ArtifactType == ArtifactType.PfdDxf)
            .Select(a => a.SimulationRunId).Distinct()
            .ToListAsync(cancellationToken);
        var pfdProjects = pfdRunIds.Where(runProject.ContainsKey).Select(id => runProject[id]).ToHashSet();

        var pids = await _db.PidDiagrams.AsNoTracking()
            .Where(x => recentIds.Contains(x.ProjectId))
            .Select(x => new { x.Id, x.ProjectId, x.Status, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var latestPid = pids.GroupBy(x => x.ProjectId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        foreach (var p in recent)
        {
            latestRuns.TryGetValue(p.Id, out var lr);
            latestPid.TryGetValue(p.Id, out var pd);
            dto.RecentProjects.Add(new DashboardProjectRowDto
            {
                ProjectId = p.Id,
                Name = p.Name,
                Country = p.LocationCountry,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                LatestRunId = lr?.Id,
                LatestRunVersion = lr?.RunVersion,
                HasLayout = layoutProjects.Contains(p.Id),
                HasPfd = pfdProjects.Contains(p.Id),
                HasPid = pd != null,
                PidStatus = pd?.Status,
                PidDiagramId = pd?.Id
            });
        }

        var pending = await _db.PidDiagrams.AsNoTracking()
            .Where(x => x.Status == PidApprovalStatus.InReview)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(6)
            .Select(x => new { x.Id, x.ProjectId, x.DrawingNo, x.RevisionNo, x.UpdatedAt, x.CreatedAt })
            .ToListAsync(cancellationToken);
        foreach (var x in pending)
        {
            dto.PendingPids.Add(new DashboardPendingPidDto
            {
                PidDiagramId = x.Id,
                ProjectId = x.ProjectId,
                ProjectName = NameOf(x.ProjectId) ?? string.Empty,
                DrawingNo = x.DrawingNo,
                RevisionNo = x.RevisionNo,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            });
        }

        var enq = await _db.Enquiries.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync(cancellationToken);
        int EnqCount(EnquiryStatus s) => enq.Where(e => e.Key == s).Select(e => e.C).FirstOrDefault();

        var props = await _db.EnquiryProposals.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync(cancellationToken);
        int PropCount(ProposalStatus s) => props.Where(e => e.Key == s).Select(e => e.C).FirstOrDefault();

        dto.Enquiries = new DashboardEnquirySummaryDto
        {
            Total = enq.Sum(e => e.C),
            New = EnqCount(EnquiryStatus.New),
            UnderReview = EnqCount(EnquiryStatus.UnderReview),
            ProposalSent = EnqCount(EnquiryStatus.ProposalSent),
            Approved = EnqCount(EnquiryStatus.Approved),
            Rejected = EnqCount(EnquiryStatus.Rejected),
            ProposalsDraft = PropCount(ProposalStatus.Draft),
            ProposalsSent = PropCount(ProposalStatus.Sent),
            ProposalsApproved = PropCount(ProposalStatus.Approved)
        };

        var asm = await _db.SiteAssessments.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync(cancellationToken);
        dto.Assessments = new DashboardAssessmentSummaryDto
        {
            Total = asm.Sum(a => a.C),
            Draft = asm.Where(a => a.Key == AssessmentStatus.Draft).Select(a => a.C).FirstOrDefault(),
            Converted = asm.Where(a => a.Key == AssessmentStatus.Converted).Select(a => a.C).FirstOrDefault()
        };

        var activity = new List<DashboardActivityDto>();

        foreach (var r in runs.OrderByDescending(r => r.CreatedAt).Take(5))
            activity.Add(new DashboardActivityDto
            {
                At = r.CreatedAt,
                Kind = "Simulation",
                Title = $"Run v{r.RunVersion} simulated",
                Detail = NameOf(r.ProjectId),
                ProjectId = r.ProjectId
            });

        var layouts = await _db.SiteLayouts.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt).Take(5)
            .Select(l => new { l.ProjectId, l.VersionNo, l.CreatedBy, l.CreatedAt })
            .ToListAsync(cancellationToken);
        foreach (var l in layouts)
            activity.Add(new DashboardActivityDto
            {
                At = l.CreatedAt,
                Kind = "Layout",
                Title = $"Site layout v{l.VersionNo} generated",
                Detail = NameOf(l.ProjectId),
                By = l.CreatedBy,
                ProjectId = l.ProjectId
            });

        var revs = await _db.PidRevisions.AsNoTracking()
            .OrderByDescending(r => r.ChangedAt).Take(5)
            .Select(r => new { r.RevisionNo, r.Status, r.ChangedBy, r.ChangedAt, ProjectId = r.PidDiagram!.ProjectId, DrawingNo = r.PidDiagram!.DrawingNo })
            .ToListAsync(cancellationToken);
        foreach (var r in revs)
            activity.Add(new DashboardActivityDto
            {
                At = r.ChangedAt,
                Kind = "P&ID",
                Title = $"{r.DrawingNo} Rev {r.RevisionNo} - {r.Status}",
                Detail = NameOf(r.ProjectId),
                By = r.ChangedBy,
                ProjectId = r.ProjectId
            });

        var jobs = await _db.CadJobs.AsNoTracking()
            .Where(j => j.Status == CadJobStatus.Completed && j.CompletedAt != null)
            .OrderByDescending(j => j.CompletedAt).Take(5)
            .Select(j => new { j.SimulationRunId, j.CompletedAt })
            .ToListAsync(cancellationToken);
        foreach (var j in jobs)
        {
            Guid? pid = runProject.TryGetValue(j.SimulationRunId, out var jp) ? jp : (Guid?)null;
            activity.Add(new DashboardActivityDto
            {
                At = j.CompletedAt!.Value,
                Kind = "CAD",
                Title = "CAD drawings completed",
                Detail = pid.HasValue ? NameOf(pid.Value) : null,
                ProjectId = pid
            });
        }

        dto.Activity = activity.OrderByDescending(a => a.At).Take(8).ToList();

        return dto;
    }

    /// <summary>Green 20-30, Yellow 15-20 or 30-35, Red below 15 or above 35.</summary>
    public static string GetCnZone(double cnRatio)
    {
        if (cnRatio is >= 20 and <= 30) return "Green";
        if (cnRatio is >= 15 and < 20 || cnRatio is > 30 and <= 35) return "Yellow";
        return "Red";
    }

    /// <summary>Reads a numeric value from a parsed-JSON dictionary (case-insensitive key); null if absent or not numeric.</summary>
    public static double? ReadNumber(IDictionary<string, object?>? values, string key)
    {
        if (values is null) return null;

        foreach (var pair in values)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            return pair.Value switch
            {
                long l => (double?)l,
                int i => (double?)i,
                double d => d,
                decimal m => (double?)m,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        return null;
    }
}