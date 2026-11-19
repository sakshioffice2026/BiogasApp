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