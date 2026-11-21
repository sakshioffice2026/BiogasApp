using System.Text;
using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Output;
using BiogasApp.Layout.Placement;
using BiogasApp.Layout.Sizing;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Generates the GA site plan DXF directly from a saved layout version (no external service),
/// saves it through IArtifactStorage and keeps cad_jobs / generated_artifacts in step.
/// Only GA_DXF is produced for now. Other formats are skipped and noted on the job.
/// Register as: services.AddScoped&lt;ICadGeneration, CadGenerationRepository&gt;();
/// </summary>
public class CadGenerationRepository : ICadGeneration
{
    // Routes the MVC CadController exposes.
    private const string StatusPollRoute = "/Cad/Status/";
    private const string DownloadRoute = "/Cad/Download/";

    private const string GaFormat = "GA_DXF";

    private static readonly Dictionary<ArtifactType, string> ArtifactToApi = new()
    {
        [ArtifactType.GaDxf] = "GA_DXF",
        [ArtifactType.PfdDxf] = "PFD_DXF",
        [ArtifactType.ShellDevDxf] = "SHELL_DEV_DXF",
        [ArtifactType.Model3dStep] = "MODEL_3D_STEP",
        [ArtifactType.BomXlsx] = "BOM_XLSX",
        [ArtifactType.ProposalPdf] = "PROPOSAL_PDF"
    };

    private readonly ApplicationDbContext _db;
    private readonly ISimulationRepository _simulations;
    private readonly ISiteRepository _sites;
    private readonly ISiteLayoutRepository _layouts;
    private readonly IClearanceRuleRepository _rules;
    private readonly IArtifactStorage _storage;
    private readonly ILogger<CadGenerationRepository> _logger;

    public CadGenerationRepository(
        ApplicationDbContext db,
        ISimulationRepository simulations,
        ISiteRepository sites,
        ISiteLayoutRepository layouts,
        IClearanceRuleRepository rules,
        IArtifactStorage storage,
        ILogger<CadGenerationRepository> logger)
    {
        _db = db;
        _simulations = simulations;
        _sites = sites;
        _layouts = layouts;
        _rules = rules;
        _storage = storage;
        _logger = logger;
    }

    /// <exception cref="KeyNotFoundException">Simulation run or project does not exist.</exception>
    /// <exception cref="ArgumentException">No valid export format requested.</exception>
    public async Task<CadJobStatusDto> QueueJobAsync(
        CadJobRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var formats = NormalizeFormats(request.ExportFormats);

        var run = await _simulations.GetRunAsync(request.SimulationRunId, cancellationToken)
            ?? throw new KeyNotFoundException($"Simulation run {request.SimulationRunId} was not found.");

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == run.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {run.ProjectId} was not found.");

        var job = new CadJob
        {
            Id = Guid.NewGuid(),
            SimulationRunId = request.SimulationRunId,
            SiteLayoutId = request.SiteLayoutId,
            Status = CadJobStatus.Running,
            ExportFormatsJson = JsonSerializer.Serialize(formats),
            CreatedAt = DateTime.UtcNow
        };

        _db.CadJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            if (!formats.Contains(GaFormat))
                throw new InvalidOperationException("Only GA_DXF is available for now. Select GA_DXF to generate.");

            if (!request.SiteLayoutId.HasValue)
                throw new InvalidOperationException("Select a saved layout version first.");

            await GenerateGaAsync(job, run, project, request.SiteLayoutId.Value, cancellationToken);

            var skipped = formats.Where(f => f != GaFormat).ToList();

            job.Status = CadJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = skipped.Count == 0
                ? null
                : $"Not available yet and skipped: {string.Join(", ", skipped)}.";

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            DetachAddedArtifacts();
            await MarkFailedAsync(job, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GA drawing generation failed for CAD job {JobId}.", job.Id);
            DetachAddedArtifacts();
            await MarkFailedAsync(job, "Could not generate the GA drawing. See the server log for details.", cancellationToken);
        }

        return ToDto(job);
    }

    public async Task<CadJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.CadJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        return job is null ? null : ToDto(job);
    }

    public Task<List<CadJob>> GetJobsByRunAsync(Guid simulationRunId, CancellationToken cancellationToken = default) =>
        _db.CadJobs
            .AsNoTracking()
            .Where(j => j.SimulationRunId == simulationRunId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>Latest artifact of each type for the run.</summary>
    public async Task<ArtifactListDto> GetArtifactsAsync(
        Guid simulationRunId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.GeneratedArtifacts
            .AsNoTracking()
            .Where(a => a.SimulationRunId == simulationRunId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestPerType = rows
            .GroupBy(a => a.ArtifactType)
            .Select(g => g.First())
            .OrderBy(a => a.ArtifactType)
            .Select(ToArtifactDto)
            .ToList();

        return new ArtifactListDto
        {
            SimulationRunId = simulationRunId,
            Artifacts = latestPerType
        };
    }

    public async Task<ArtifactDto?> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await _db.GeneratedArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artifactId, cancellationToken);

        return artifact is null ? null : ToArtifactDto(artifact);
    }

    // ------------------------------------------------------------------ generation

    private async Task GenerateGaAsync(
        CadJob job,
        SimulationRun run,
        Project project,
        Guid layoutId,
        CancellationToken cancellationToken)
    {
        var layout = await _layouts.GetByIdAsync(layoutId, cancellationToken)
            ?? throw new InvalidOperationException("The selected layout version was not found.");

        if (layout.ProjectId != project.Id)
            throw new InvalidOperationException("The selected layout version belongs to a different project.");

        var site = await _sites.GetByProjectAsync(project.Id, cancellationToken)
            ?? throw new InvalidOperationException("Enter the plot first.");

        var plotErrors = PlotValidator.Validate(site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList());
        if (plotErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", plotErrors));

        var equipment = await _sites.GetEquipmentAsync(project.Id, cancellationToken);
        var provider = await _rules.CreateProviderAsync(layout.RuleSetId, cancellationToken);

        var input = BuildInput(site, equipment);
        var items = ToPlaced(layout.Items, equipment);
        var result = new PlacementEngine(provider).Evaluate(input, items);
        var roads = RoadPlanner.Plan(input, ZoneAssigner.Assign(input), provider).Roads;

        var ruleSetName = await _db.ClearanceRuleSets
            .AsNoTracking()
            .Where(r => r.Id == provider.RuleSetId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Clearance rules";

        var info = new TitleBlockInfo(
            ProjectName: project.Name,
            Location: $"{project.LocationCountry} ({site.Latitude:0.0000}, {site.Longitude:0.0000})",
            CapacityText: $"{run.NetCbgKgDay / 1000m:0.#} TPD CBG ({run.TotalFeedTpd:0.#} TPD feed)",
            DrawingNo: $"GA-{project.Id.ToString()[..8].ToUpperInvariant()}-V{layout.VersionNo}",
            Revision: $"R{layout.VersionNo}",
            Date: DateTime.UtcNow,
            RuleSetName: ruleSetName,
            RuleSetVersion: provider.RuleSetVersion,
            LayoutOriginText: string.Equals(layout.Origin, "Manual", StringComparison.OrdinalIgnoreCase)
                ? "Manually edited layout"
                : "Auto-generated layout",
            WarningSummary: BuildWarningSummary(result.Warnings));

        var bytes = SiteDxfWriter.Write(input, result, roads, info);
        var fileName = $"GA_{SafeName(project.Name)}_V{layout.VersionNo}_{DateTime.UtcNow:yyyyMMddHHmm}.dxf";

        await using var content = new MemoryStream(bytes);
        var fileUrl = await _storage.SaveAsync(job.SimulationRunId, fileName, content, cancellationToken);

        await using var saved = await _storage.OpenReadAsync(fileUrl, cancellationToken)
            ?? throw new IOException($"Saved artifact '{fileUrl}' could not be read back.");

        var checksum = await _storage.ComputeSha256Async(saved, cancellationToken);

        _db.GeneratedArtifacts.Add(new GeneratedArtifact
        {
            Id = Guid.NewGuid(),
            SimulationRunId = job.SimulationRunId,
            ArtifactType = ArtifactType.GaDxf,
            FileUrl = fileUrl,
            Checksum = checksum,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static LayoutInput BuildInput(SiteDto site, List<EquipmentItemDto> equipment)
    {
        var geo = new GeoProjection(site.Latitude, site.Longitude);

        var entities = equipment.Select(e => new ProjectEquipmentItem
        {
            Id = e.Id ?? Guid.Empty,
            ProjectId = e.ProjectId,
            Tag = e.Tag,
            Name = e.Name,
            HazardClass = ParseEnum(e.HazardClass, HazardClass.Other),
            Zone = ParseEnum(e.Zone, EquipmentZone.Process),
            Shape = ParseEnum(e.Shape, EquipmentShape.Rectangle),
            WidthM = (decimal?)e.WidthM,
            LengthM = (decimal?)e.LengthM,
            DiameterM = (decimal?)e.DiameterM,
            Quantity = e.Quantity,
            IsEnabled = e.IsEnabled,
            AllowRotation = e.AllowRotation,
            NeedsRoadAccess = e.NeedsRoadAccess
        });

        return new LayoutInput
        {
            PlotCorners = site.PlotCorners.Select(c => new Point2D(c.X, c.Y)).ToList(),
            GateEdgeIndex = site.GateEdgeIndex,
            GateOffsetM = site.GateOffsetM,
            NorthAngleDeg = site.NorthAngleDeg,
            WindFromDeg = site.WindFromDeg,
            SlopeDirectionDeg = site.SlopeDirectionDeg,
            PowerSideDeg = site.PowerSideDeg,
            GasOfftakeSideDeg = site.GasOfftakeSideDeg,
            Equipment = FootprintBuilder.FromItems(entities),
            NoBuildZones = site.NoBuildZones
                .Where(z => z.Polygon.Count >= 3)
                .Select(z => new NoBuildInput(
                    ParseEnum(z.Reason, NoBuildReason.Other),
                    z.Polygon.Select(p => new Point2D(p.X, p.Y)).ToList(),
                    z.Notes))
                .ToList(),
            Receptors = site.Receptors
                .Where(r => r.IsConfirmed)
                .Select(r => new ReceptorInput(
                    ParseEnum(r.Type, ReceptorType.Other),
                    r.Name,
                    geo.ToMetres(r.Latitude, r.Longitude),
                    r.DistanceFromPlotM))
                .ToList(),
            LockedItems = new List<LockedPlacement>()
        };
    }

    private static List<PlacedItem> ToPlaced(IEnumerable<LayoutItemDto> items, List<EquipmentItemDto> equipment)
    {
        var byId = equipment.Where(e => e.Id.HasValue).ToDictionary(e => e.Id!.Value);

        return items.Select(i =>
        {
            byId.TryGetValue(i.EquipmentItemId, out var e);
            return new PlacedItem
            {
                EquipmentId = i.EquipmentItemId,
                InstanceNo = i.InstanceNo,
                Tag = i.Tag,
                Name = i.Name,
                HazardClass = ParseEnum(e?.HazardClass, HazardClass.Other),
                Zone = ParseEnum(i.Zone, EquipmentZone.Process),
                Shape = ParseEnum(i.Shape, EquipmentShape.Rectangle),
                X = i.X,
                Y = i.Y,
                WidthM = i.WidthM,
                LengthM = i.LengthM,
                RotationDeg = i.RotationDeg,
                IsLocked = i.IsLocked,
                NeedsRoadAccess = e?.NeedsRoadAccess ?? true
            };
        }).ToList();
    }

    private static string BuildWarningSummary(IReadOnlyCollection<LayoutWarning> warnings)
    {
        if (warnings.Count == 0) return "No warnings";

        int official = warnings.Count(w => w.RuleStatus == RuleStatus.Official);
        int reference = warnings.Count(w => w.RuleStatus == RuleStatus.Reference);
        int assumed = warnings.Count(w => w.RuleStatus == RuleStatus.Assumed);
        int other = warnings.Count - official - reference - assumed;

        var sb = new StringBuilder();
        sb.Append($"{warnings.Count} warning(s): {official} Official, {reference} Reference, {assumed} Assumed");
        if (other > 0) sb.Append($", {other} other");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ helpers

    private async Task MarkFailedAsync(CadJob job, string message, CancellationToken cancellationToken)
    {
        job.Status = CadJobStatus.Failed;
        job.ErrorMessage = message;
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // A failed job leaves no artifact row behind.
    private void DetachAddedArtifacts()
    {
        foreach (var entry in _db.ChangeTracker.Entries<GeneratedArtifact>()
                     .Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }

    private static List<string> NormalizeFormats(IEnumerable<string>? requested)
    {
        var formats = (requested ?? Enumerable.Empty<string>())
            .Select(f => (f ?? string.Empty).Trim())
            .Where(f => f.Length > 0)
            .Select(f => CadViewModel.AvailableFormats.FirstOrDefault(
                a => a.Equals(f, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        if (formats.Count == 0 || formats.Any(f => f is null))
            throw new ArgumentException(
                $"Export formats must be one or more of: {string.Join(", ", CadViewModel.AvailableFormats)}.",
                nameof(requested));

        return formats!;
    }

    private static string SafeName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');

        var cleaned = sb.ToString().Trim('_');
        if (cleaned.Length > 40) cleaned = cleaned[..40];
        return cleaned.Length == 0 ? "Project" : cleaned;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) ? result : fallback;

    private static CadJobStatusDto ToDto(CadJob job) => new()
    {
        JobId = job.Id,
        SimulationRunId = job.SimulationRunId,
        Status = job.Status switch
        {
            CadJobStatus.Running => "RUNNING",
            CadJobStatus.Completed => "COMPLETED",
            CadJobStatus.Failed => "FAILED",
            _ => "QUEUED"
        },
        EstimatedTimeSeconds = 0,
        StatusPollUrl = $"{StatusPollRoute}{job.Id}",
        ErrorMessage = job.ErrorMessage
    };

    private static ArtifactDto ToArtifactDto(GeneratedArtifact a) => new()
    {
        ArtifactId = a.Id,
        ArtifactType = ArtifactToApi.GetValueOrDefault(a.ArtifactType, a.ArtifactType.ToString()),
        FileName = Path.GetFileName(a.FileUrl),
        DownloadUrl = $"{DownloadRoute}{a.Id}",
        ChecksumSha256 = a.Checksum,
        CreatedAt = a.CreatedAt
    };
}