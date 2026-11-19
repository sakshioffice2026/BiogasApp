using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BiogasApp.Repositories.Repositories;

/// <summary>
/// Talks to the Python CadService and keeps cad_jobs / generated_artifacts in step.
///
/// Register as a typed client (BaseAddress must end with "/"):
///   services.AddHttpClient&lt;ICadGeneration, CadGenerationRepository&gt;(c =>
///       c.BaseAddress = new Uri("http://localhost:8000/"));
///
/// CadService contract this class expects (camelCase JSON):
///   POST cad/start                        body: { jobId, simulationRunId, exportFormats[], dxfOptions,
///                                                 project{...}, simulation{...} }   -> 2xx = accepted
///   GET  cad/status/{jobId}               -> { status: QUEUED|RUNNING|COMPLETED|FAILED,
///                                             errorMessage, files:[{ artifactType, fileName }] }  (404 = unknown job)
///   GET  cad/download/{jobId}/{fileName}  -> raw file bytes
/// The CadJob id is used as the CadService job id, so no extra id mapping is stored.
/// </summary>
public class CadGenerationRepository : ICadGeneration
{
    // Routes the MVC CadController is expected to expose. Change here if you pick different ones.
    private const string StatusPollRoute = "/Cad/Status/";
    private const string DownloadRoute = "/Cad/Download/";

    private const int EstimatedSeconds = 15;
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<ArtifactType, string> ArtifactToApi = new()
    {
        [ArtifactType.GaDxf] = "GA_DXF",
        [ArtifactType.PfdDxf] = "PFD_DXF",
        [ArtifactType.ShellDevDxf] = "SHELL_DEV_DXF",
        [ArtifactType.Model3dStep] = "MODEL_3D_STEP",
        [ArtifactType.BomXlsx] = "BOM_XLSX",
        [ArtifactType.ProposalPdf] = "PROPOSAL_PDF"
    };

    private static readonly Dictionary<string, ArtifactType> ApiToArtifact =
        ArtifactToApi.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ISimulationRepository _simulations;
    private readonly IArtifactStorage _storage;
    private readonly ILogger<CadGenerationRepository> _logger;

    public CadGenerationRepository(
        HttpClient http,
        ApplicationDbContext db,
        ISimulationRepository simulations,
        IArtifactStorage storage,
        ILogger<CadGenerationRepository> logger)
    {
        _http = http;
        _db = db;
        _simulations = simulations;
        _storage = storage;
        _logger = logger;
    }

    /// <exception cref="KeyNotFoundException">Simulation run does not exist.</exception>
    /// <exception cref="ArgumentException">No valid export format requested.</exception>
    public async Task<CadJobStatusDto> QueueJobAsync(
        CadJobRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var formats = NormalizeFormats(request.ExportFormats);

        var result = await _simulations.GetResultDtoAsync(request.SimulationRunId, cancellationToken)
            ?? throw new KeyNotFoundException($"Simulation run {request.SimulationRunId} was not found.");

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == result.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {result.ProjectId} was not found.");

        var job = new CadJob
        {
            Id = Guid.NewGuid(),
            SimulationRunId = request.SimulationRunId,
            Status = CadJobStatus.Queued,
            ExportFormatsJson = JsonSerializer.Serialize(formats),
            CreatedAt = DateTime.UtcNow
        };

        _db.CadJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            jobId = job.Id,
            simulationRunId = job.SimulationRunId,
            exportFormats = formats,
            dxfOptions = request.DxfOptions,
            project = new
            {
                id = project.Id,
                name = project.Name,
                regulatoryStandard = project.RegulatoryStandard == RegulatoryStandard.PesoIndia ? "PESO_INDIA" : "ATEX_EU",
                ambientTempMinC = project.AmbientTempMinC,
                ambientTempMaxC = project.AmbientTempMaxC
            },
            simulation = result
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("cad/start", payload, Json, cancellationToken);

            if (!response.IsSuccessStatusCode)
                await MarkFailedAsync(job, $"CAD service rejected the job (HTTP {(int)response.StatusCode}).", cancellationToken);
        }
        catch (Exception ex) when (IsServiceFailure(ex, cancellationToken))
        {
            _logger.LogError(ex, "Could not reach the CAD service for job {JobId}.", job.Id);
            await MarkFailedAsync(job, "CAD service is not reachable.", cancellationToken);
        }

        return ToDto(job);
    }

    public async Task<CadJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.CadJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null) return null;

        if (job.Status is CadJobStatus.Completed or CadJobStatus.Failed)
            return ToDto(job);

        if (DateTime.UtcNow - job.CreatedAt > JobTimeout)
        {
            await MarkFailedAsync(job, "Timed out waiting for the CAD service.", cancellationToken);
            return ToDto(job);
        }

        CadStatusResponse? remote;

        try
        {
            using var response = await _http.GetAsync($"cad/status/{job.Id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await MarkFailedAsync(job, "CAD service no longer knows this job (it may have restarted).", cancellationToken);
                return ToDto(job);
            }

            response.EnsureSuccessStatusCode();
            remote = await response.Content.ReadFromJsonAsync<CadStatusResponse>(Json, cancellationToken);
        }
        catch (Exception ex) when (IsServiceFailure(ex, cancellationToken))
        {
            // Transient: keep the job as it is and let the next poll try again.
            _logger.LogWarning(ex, "Could not read status of CAD job {JobId}.", job.Id);
            return ToDto(job);
        }

        switch (remote?.Status?.Trim().ToUpperInvariant())
        {
            case "FAILED":
                await MarkFailedAsync(job, remote!.ErrorMessage ?? "CAD generation failed.", cancellationToken);
                break;

            case "COMPLETED":
                await CompleteJobAsync(job, remote!.Files ?? new List<CadFileInfo>(), cancellationToken);
                break;

            case "RUNNING":
                if (job.Status != CadJobStatus.Running)
                {
                    job.Status = CadJobStatus.Running;
                    await _db.SaveChangesAsync(cancellationToken);
                }
                break;
        }

        return ToDto(job);
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

    // ------------------------------------------------------------------ helpers

    private async Task CompleteJobAsync(
        CadJob job,
        List<CadFileInfo> files,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var file in files)
            {
                if (!ApiToArtifact.TryGetValue(file.ArtifactType ?? string.Empty, out var artifactType))
                {
                    _logger.LogWarning("CAD job {JobId}: unknown artifact type '{Type}' skipped.", job.Id, file.ArtifactType);
                    continue;
                }

                var url = $"cad/download/{job.Id}/{Uri.EscapeDataString(file.FileName)}";

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
                var fileUrl = await _storage.SaveAsync(job.SimulationRunId, file.FileName, download, cancellationToken);

                await using var saved = await _storage.OpenReadAsync(fileUrl, cancellationToken)
                    ?? throw new IOException($"Saved artifact '{fileUrl}' could not be read back.");

                var checksum = await _storage.ComputeSha256Async(saved, cancellationToken);

                _db.GeneratedArtifacts.Add(new GeneratedArtifact
                {
                    Id = Guid.NewGuid(),
                    SimulationRunId = job.SimulationRunId,
                    ArtifactType = artifactType,
                    FileUrl = fileUrl,
                    Checksum = checksum,
                    CreatedAt = DateTime.UtcNow
                });
            }

            job.Status = CadJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _logger.LogError(ex, "Importing artifacts of CAD job {JobId} failed.", job.Id);

            // Drop any artifact rows added in this attempt so a failed job leaves no partial set.
            foreach (var entry in _db.ChangeTracker.Entries<GeneratedArtifact>()
                         .Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;

            await MarkFailedAsync(job, "Could not download the generated files from the CAD service.", cancellationToken);
        }
    }

    private async Task MarkFailedAsync(CadJob job, string message, CancellationToken cancellationToken)
    {
        job.Status = CadJobStatus.Failed;
        job.ErrorMessage = message;
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
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

    private static bool IsServiceFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException ||
        ex is JsonException ||
        (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested); // HttpClient timeout

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
        EstimatedTimeSeconds = job.Status is CadJobStatus.Completed or CadJobStatus.Failed ? 0 : EstimatedSeconds,
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

    private sealed record CadStatusResponse(string? Status, string? ErrorMessage, List<CadFileInfo>? Files);

    private sealed record CadFileInfo(string? ArtifactType, string FileName);
}