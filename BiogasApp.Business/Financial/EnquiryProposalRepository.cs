using BiogasApp.Business.Engine;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BiogasApp.Business.Financial;

/// <summary>
/// Creates enquiry proposals: pre-sales Project + feedstock mix + engine run + PDF/BOM export + proposal record.
/// Lives in the Business project because it needs ICalculationEngine (Repositories cannot reference Business).
///
/// Register:  services.AddScoped&lt;IEnquiryProposalRepository, EnquiryProposalRepository&gt;();
/// </summary>
public class EnquiryProposalRepository : IEnquiryProposalRepository
{
    private readonly ApplicationDbContext _db;
    private readonly IProjectRepository _projects;
    private readonly IFeedstockRepository _feedstocks;
    private readonly ISimulationRepository _simulations;
    private readonly IProposalExport _export;
    private readonly ICalculationEngine _engine;
    private readonly IArtifactStorage _storage;
    private readonly ILogger<EnquiryProposalRepository> _logger;

    public EnquiryProposalRepository(
        ApplicationDbContext db,
        IProjectRepository projects,
        IFeedstockRepository feedstocks,
        ISimulationRepository simulations,
        IProposalExport export,
        ICalculationEngine engine,
        IArtifactStorage storage,
        ILogger<EnquiryProposalRepository> logger)
    {
        _db = db;
        _projects = projects;
        _feedstocks = feedstocks;
        _simulations = simulations;
        _export = export;
        _engine = engine;
        _storage = storage;
        _logger = logger;
    }

    // ------------------------------------------------------------------ form

    public async Task<EnquiryProposalFormViewModel?> GetFormAsync(
        Guid enquiryId,
        CancellationToken cancellationToken = default)
    {
        var enquiry = await _db.Enquiries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enquiryId, cancellationToken);
        if (enquiry == null)
        {
            return null;
        }

        var latest = await _db.EnquiryProposals
            .AsNoTracking()
            .Where(p => p.EnquiryId == enquiryId)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var allFeedstocks = await _feedstocks.GetAllAsync(null, null, cancellationToken);

        var model = new EnquiryProposalFormViewModel
        {
            EnquiryId = enquiry.Id,
            EnquiryNo = enquiry.EnquiryNo,
            CustomerName = enquiry.CustomerName,
            FeedstockRequested = enquiry.FeedstockType,
            QuantityRequestedTpd = enquiry.FeedstockQtyTpd,
            EndUseRequested = enquiry.EndUse,
            NextVersion = (latest?.Version ?? 0) + 1,
            ProjectId = latest?.ProjectId,
            AvailableFeedstocks = allFeedstocks
                .OrderBy(f => f.Name)
                .Select(f => new FeedstockOptionViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    Category = f.Category.ToString()
                })
                .ToList()
        };


        model.AssessmentId = latest?.AssessmentId
            ?? await _db.SiteAssessments
                .AsNoTracking()
                .Where(a => a.EnquiryId == enquiryId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (latest != null)
        {
            var project = await _projects.GetByIdAsync(latest.ProjectId, cancellationToken);
            if (project != null)
            {
                model.RegulatoryStandard = project.RegulatoryStandard;
                model.AmbientTempMinC = project.AmbientTempMinC;
                model.AmbientTempMaxC = project.AmbientTempMaxC;
                model.DigesterRegime = project.DigesterRegime;
                model.UpgradingTech = project.UpgradingTech;
            }

            var mix = await _projects.GetFeedstockMixAsync(latest.ProjectId, cancellationToken);
            model.FeedstockLines = mix.Select(m => new ProposalFeedstockLineViewModel
            {
                FeedstockId = m.FeedstockId,
                DailyTonnageTpd = m.DailyTonnageTpd,
                SeasonalAvailabilityDays = m.SeasonalAvailabilityDays
            }).ToList();
        }
        else
        {
            var requested = enquiry.FeedstockType.Trim();
            var match = allFeedstocks.FirstOrDefault(f =>
                requested.Contains(f.Name, StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains(requested, StringComparison.OrdinalIgnoreCase));

            model.FeedstockLines.Add(new ProposalFeedstockLineViewModel
            {
                FeedstockId = match?.Id ?? Guid.Empty,
                DailyTonnageTpd = enquiry.FeedstockQtyTpd,
                SeasonalAvailabilityDays = 365
            });
        }

        return model;
    }

    // ------------------------------------------------------------------ create

    /// <exception cref="KeyNotFoundException">Enquiry not found.</exception>
    /// <exception cref="ArgumentException">No valid feedstock line.</exception>
    public async Task<EnquiryProposal> CreateAsync(
        EnquiryProposalFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var enquiry = await _db.Enquiries
            .FirstOrDefaultAsync(e => e.Id == model.EnquiryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Enquiry {model.EnquiryId} was not found.");

        var lines = (model.FeedstockLines ?? new List<ProposalFeedstockLineViewModel>())
            .Where(l => l.FeedstockId != Guid.Empty && l.DailyTonnageTpd > 0)
            .ToList();

        if (lines.Count == 0)
        {
            throw new ArgumentException("Add at least one feedstock with a quantity above zero.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var previous = await _db.EnquiryProposals
            .Where(p => p.EnquiryId == enquiry.Id)
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        var version = (previous.FirstOrDefault()?.Version ?? 0) + 1;
        var assessmentId = model.AssessmentId
    ?? await _db.SiteAssessments
        .Where(a => a.EnquiryId == enquiry.Id)
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => (Guid?)a.Id)
        .FirstOrDefaultAsync(cancellationToken);

        var assessment = assessmentId.HasValue
            ? await _db.SiteAssessments.FirstOrDefaultAsync(
                a => a.Id == assessmentId.Value && a.EnquiryId == enquiry.Id, cancellationToken)
            : null;

        if (assessment == null)
        {
            throw new InvalidOperationException("Create a site assessment for this enquiry first.");
        }

        if (assessment.Status == AssessmentStatus.Converted)
        {
            throw new InvalidOperationException("This assessment is already converted to a project.");
        }
        // 1. pre-sales project (reuse the one from the previous version)
        var project = await EnsureProjectAsync(enquiry, previous.FirstOrDefault()?.ProjectId ?? assessment.ProjectId, model, cancellationToken);
        assessment.ProjectId = project.Id;
        assessment.UpdatedAt = DateTime.UtcNow;
        // 2. feedstock mix
        var mix = lines.Select(l => new ProjectFeedstockMix
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            FeedstockId = l.FeedstockId,
            DailyTonnageTpd = l.DailyTonnageTpd,
            SeasonalAvailabilityDays = l.SeasonalAvailabilityDays <= 0 ? 365 : l.SeasonalAvailabilityDays
        }).ToList();

        await _projects.SaveFeedstockMixAsync(project.Id, mix, cancellationToken);

        // 3. engine run
        var feedstocks = await _feedstocks.GetByIdsAsync(mix.Select(m => m.FeedstockId), cancellationToken);
        var inputs = SimulationRunBuilder.BuildInputs(mix, feedstocks);

        var result = _engine.Run(new CalculationInput(
            inputs,
            project.DigesterRegime,
            project.UpgradingTech,
            (double)project.AmbientTempMinC));

        var runVersion = await _simulations.GetNextRunVersionAsync(project.Id, cancellationToken);
        var run = SimulationRunBuilder.BuildRun(project.Id, runVersion, result);
        var equipment = SimulationRunBuilder.BuildEquipment(run.Id, project.UpgradingTech, result);
        await _simulations.AddRunAsync(run, equipment, cancellationToken);

        if (project.Status == ProjectStatus.Draft)
        {
            await _projects.SetStatusAsync(project.Id, ProjectStatus.Simulated, cancellationToken);
        }

        // 4. PDF + BOM + financial metrics
        var export = await _export.ExportProposalAsync(new ProposalRequestDto
        {
            SimulationRunId = run.Id,
            FinancialInputs = model.FinancialInputs ?? new FinancialInputsDto(),
            CustomRateCardOverrides = new List<RateCardOverrideDto>()
        }, cancellationToken);

        var metrics = export.FinancialMetrics;

        // 5. supersede open earlier versions
        foreach (var old in previous.Where(p => p.Status is ProposalStatus.Draft or ProposalStatus.Sent))
        {
            old.Status = ProposalStatus.Superseded;
        }

        // 6. proposal record
        var proposal = new EnquiryProposal
        {
            Id = Guid.NewGuid(),
            EnquiryId = enquiry.Id,
            ProposalNo = $"{enquiry.EnquiryNo.Replace("ENQ", "PRP", StringComparison.Ordinal)}-V{version}",
            Version = version,
            ProjectId = project.Id,
            SimulationRunId = run.Id,
            AssessmentId = assessment.Id,
            Status = ProposalStatus.Draft,
            CapexTotal = (decimal)metrics.CapexTotal,
            OpexAnnual = (decimal)metrics.OpexAnnual,
            AnnualRevenue = (decimal)metrics.AnnualRevenue,
            Npv = (decimal)metrics.Npv,
            IrrPercent = metrics.IrrPercent.HasValue ? (decimal)metrics.IrrPercent.Value : null,
            PaybackPeriodYears = metrics.PaybackPeriodYears.HasValue ? (decimal)metrics.PaybackPeriodYears.Value : null,
            ProposalPdfUrl = export.ProposalPdfUrl,
            ExcelBomUrl = export.ExcelBomUrl,
            Remarks = model.Remarks,
            CreatedAt = DateTime.UtcNow
        };

        _db.EnquiryProposals.Add(proposal);

        // 7. enquiry status
        if (enquiry.Status is EnquiryStatus.New)
        {
            enquiry.Status = EnquiryStatus.UnderReview;
        }
        else if (version > 1 && enquiry.Status is EnquiryStatus.ProposalSent or EnquiryStatus.UnderReview)
        {
            enquiry.Status = EnquiryStatus.Revised;
        }

        enquiry.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("Proposal {ProposalNo} created for enquiry {EnquiryNo}.", proposal.ProposalNo, enquiry.EnquiryNo);
        return proposal;
    }

    private async Task<Project> EnsureProjectAsync(
        Enquiry enquiry,
        Guid? existingProjectId,
        EnquiryProposalFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (existingProjectId.HasValue)
        {
            var existing = await _projects.GetByIdAsync(existingProjectId.Value, cancellationToken);
            if (existing != null)
            {
                existing.RegulatoryStandard = model.RegulatoryStandard;
                existing.AmbientTempMinC = model.AmbientTempMinC;
                existing.AmbientTempMaxC = model.AmbientTempMaxC;
                existing.DigesterRegime = model.DigesterRegime;
                existing.UpgradingTech = model.UpgradingTech;

                await _projects.UpdateAsync(existing, cancellationToken);
                return existing;
            }
        }

        var name = $"{enquiry.EnquiryNo} - {enquiry.CustomerName}";
        if (name.Length > 150)
        {
            name = name[..150];
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            LocationCountry = enquiry.LocationCountry,
            RegulatoryStandard = model.RegulatoryStandard,
            AmbientTempMinC = model.AmbientTempMinC,
            AmbientTempMaxC = model.AmbientTempMaxC,
            DigesterRegime = model.DigesterRegime,
            UpgradingTech = model.UpgradingTech,
            Status = ProjectStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        return await _projects.AddAsync(project, cancellationToken);
    }

    // ------------------------------------------------------------------ read

    public Task<EnquiryProposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.EnquiryProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<EnquiryProposalDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var p = await _db.EnquiryProposals
            .AsNoTracking()
            .Include(x => x.Enquiry)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null || p.Enquiry == null)
        {
            return null;
        }

        return new EnquiryProposalDetailsViewModel
        {
            Id = p.Id,
            EnquiryId = p.EnquiryId,
            EnquiryNo = p.Enquiry.EnquiryNo,
            CustomerName = p.Enquiry.CustomerName,
            ProjectId = p.ProjectId,
            SimulationRunId = p.SimulationRunId,
            ProposalNo = p.ProposalNo,
            Version = p.Version,
            Status = p.Status,
            CapexTotal = p.CapexTotal,
            OpexAnnual = p.OpexAnnual,
            AnnualRevenue = p.AnnualRevenue,
            Npv = p.Npv,
            IrrPercent = p.IrrPercent,
            PaybackPeriodYears = p.PaybackPeriodYears,
            Remarks = p.Remarks,
            HasPdf = !string.IsNullOrWhiteSpace(p.ProposalPdfUrl),
            HasBom = !string.IsNullOrWhiteSpace(p.ExcelBomUrl),
            CreatedAt = p.CreatedAt,
            SentAt = p.SentAt,
            DecidedAt = p.DecidedAt,
            Versions = await GetByEnquiryAsync(p.EnquiryId, cancellationToken)
        };
    }

    public Task<List<EnquiryProposalListItemViewModel>> GetByEnquiryAsync(
        Guid enquiryId,
        CancellationToken cancellationToken = default) =>
        _db.EnquiryProposals
            .AsNoTracking()
            .Where(p => p.EnquiryId == enquiryId)
            .OrderByDescending(p => p.Version)
            .Select(p => new EnquiryProposalListItemViewModel
            {
                Id = p.Id,
                ProposalNo = p.ProposalNo,
                Version = p.Version,
                Status = p.Status,
                CapexTotal = p.CapexTotal,
                IrrPercent = p.IrrPercent,
                PaybackPeriodYears = p.PaybackPeriodYears,
                CreatedAt = p.CreatedAt,
                SentAt = p.SentAt
            })
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------ status

    /// <exception cref="KeyNotFoundException">Proposal not found.</exception>
    /// <exception cref="InvalidOperationException">Proposal is superseded.</exception>
    public async Task SetStatusAsync(
        Guid id,
        ProposalStatus status,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _db.EnquiryProposals
            .Include(p => p.Enquiry)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Proposal {id} was not found.");

        if (proposal.Status == ProposalStatus.Superseded)
        {
            throw new InvalidOperationException("A superseded proposal cannot be changed. Use the latest version.");
        }

        if (status == ProposalStatus.Superseded)
        {
            throw new InvalidOperationException("Superseded is set automatically when a new version is created.");
        }

        proposal.Status = status;
        proposal.Remarks = remarks;

        var now = DateTime.UtcNow;
        if (status == ProposalStatus.Sent && proposal.SentAt == null)
        {
            proposal.SentAt = now;
        }

        if (status is ProposalStatus.Approved or ProposalStatus.Rejected)
        {
            proposal.DecidedAt = now;
        }

        if (proposal.Enquiry != null)
        {
            proposal.Enquiry.UpdatedAt = now;
            proposal.Enquiry.Status = status switch
            {
                ProposalStatus.Sent => EnquiryStatus.ProposalSent,
                ProposalStatus.Approved => EnquiryStatus.Approved,
                ProposalStatus.Rejected => EnquiryStatus.Rejected,
                _ => proposal.Enquiry.Status
            };
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ pdf

    public async Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var proposal = await _db.EnquiryProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (proposal == null || string.IsNullOrWhiteSpace(proposal.ProposalPdfUrl))
        {
            return null;
        }

        var lastSegment = proposal.ProposalPdfUrl.TrimEnd('/').Split('/').Last();
        if (!Guid.TryParse(lastSegment, out var artifactId))
        {
            return null;
        }

        var artifact = await _db.GeneratedArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artifactId, cancellationToken);
        if (artifact == null)
        {
            return null;
        }

        await using var stream = await _storage.OpenReadAsync(artifact.FileUrl, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
