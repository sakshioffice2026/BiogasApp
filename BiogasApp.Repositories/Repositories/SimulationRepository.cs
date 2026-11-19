using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class SimulationRepository : ISimulationRepository
{
    private const double SlurryDensityTPerM3 = 1.02; // same density the blending step uses

    private readonly ApplicationDbContext _db;

    public SimulationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetNextRunVersionAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var max = await _db.SimulationRuns
            .Where(r => r.ProjectId == projectId)
            .MaxAsync(r => (int?)r.RunVersion, cancellationToken);

        return (max ?? 0) + 1;
    }

    /// <summary>
    /// Saves a run and its sized equipment in one SaveChanges (atomic).
    /// If run.RunVersion is 0 or less, the next version number for the project is assigned.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Project does not exist.</exception>
    public async Task<SimulationRun> AddRunAsync(
        SimulationRun run,
        IEnumerable<SizedEquipment> equipment,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == run.ProjectId, cancellationToken))
            throw new KeyNotFoundException($"Project {run.ProjectId} was not found.");

        if (run.Id == Guid.Empty)
            run.Id = Guid.NewGuid();

        if (run.RunVersion <= 0)
            run.RunVersion = await GetNextRunVersionAsync(run.ProjectId, cancellationToken);

        if (string.IsNullOrWhiteSpace(run.WarningsJson))
            run.WarningsJson = "[]";

        run.CreatedAt = DateTime.UtcNow;
        _db.SimulationRuns.Add(run);

        foreach (var item in equipment)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.SimulationRunId = run.Id;

            if (string.IsNullOrWhiteSpace(item.ProcessDutyJson))
                item.ProcessDutyJson = "{}";

            _db.SizedEquipment.Add(item);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public Task<SimulationRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _db.SimulationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

    public Task<SimulationRun?> GetLatestRunAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _db.SimulationRuns
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RunVersion)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<SimulationRun>> GetRunsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _db.SimulationRuns
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RunVersion)
            .ToListAsync(cancellationToken);

    public Task<List<SizedEquipment>> GetEquipmentAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _db.SizedEquipment
            .AsNoTracking()
            .Where(e => e.SimulationRunId == runId)
            .OrderBy(e => e.EquipmentTag)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Builds the API/UI result for a stored run.
    /// DilutionWaterRequiredTpd and TotalSlurryFlowM3Day are not stored in simulation_runs,
    /// so they are derived from stored values: Q = VS(kg/day) / (OLR * HRT), and
    /// dilution water = Q * density - total feed (never below 0).
    /// </summary>
    public async Task<SimulationResultDto?> GetResultDtoAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runId, cancellationToken);
        if (run is null) return null;

        var equipment = await GetEquipmentAsync(runId, cancellationToken);

        var vsKgDay = (double)run.BlendedVsTpd * 1000.0;
        var olr = (double)run.OlrKgVsM3Day;
        var hrt = (double)run.HrtDays;
        var totalFeed = (double)run.TotalFeedTpd;

        var slurryFlowM3Day = olr > 0 && hrt > 0 ? vsKgDay / (olr * hrt) : 0.0;
        var dilutionWaterTpd = Math.Max(0.0, (slurryFlowM3Day * SlurryDensityTPerM3) - totalFeed);

        return new SimulationResultDto
        {
            SimulationRunId = run.Id,
            ProjectId = run.ProjectId,
            Version = run.RunVersion,
            MassBalance = new MassBalanceDto
            {
                TotalFeedTpd = totalFeed,
                BlendedTsPercent = (double)run.BlendedTsPercent,
                DilutionWaterRequiredTpd = Math.Round(dilutionWaterTpd, 2),
                TotalSlurryFlowM3Day = Math.Round(slurryFlowM3Day, 2),
                BlendedCnRatio = (double)run.BlendedCnRatio,
                VsTotalKgDay = vsKgDay
            },
            DigesterKinetics = new DigesterKineticsDto
            {
                HrtDays = hrt,
                OlrKgVsM3Day = olr,
                RawBiogasNm3Day = (double)run.RawBiogasNm3Day,
                Ch4Percent = (double)run.Ch4Percent,
                Co2Percent = (double)run.Co2Percent,
                H2sPpm = (double)run.H2sPpm,
                NetCbgKgDay = (double)run.NetCbgKgDay
            },
            EquipmentSizing = equipment.Select(e => new EquipmentSizingDto
            {
                EquipmentTag = e.EquipmentTag,
                EquipmentType = e.EquipmentType,
                Quantity = e.Quantity,
                GeometryParams = ParseObject(e.GeometryParamsJson),
                ProcessDuty = ParseObject(e.ProcessDutyJson),
                UnitCost = e.UnitCost,
                TotalCost = e.TotalCost
            }).ToList(),
            Warnings = ParseStringList(run.WarningsJson)
        };
    }

    private static Dictionary<string, object?> ParseObject(string? json)
    {
        var result = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = ToClr(prop.Value);
        }
        catch (JsonException)
        {
            // Malformed JSON in the column: return what we have (empty) rather than fail the page.
        }

        return result;
    }

    private static List<string> ParseStringList(string? json)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrEmpty(text)) result.Add(text);
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    /// <summary>Converts a JsonElement into plain CLR values (long/double/string/bool/list/dictionary).</summary>
    private static object? ToClr(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ToClr).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
        _ => null
    };
}