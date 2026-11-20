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
    private const string UpgradingSkidSuffix = "_UPGRADING_SKID";

    // EquipmentType values written by SimulationController.BuildEquipment.
    public const string HeatExchangerType = "HEAT_EXCHANGER";
    public const string CompressorType = "HIGH_PRESSURE_COMPRESSOR";
    public const string CascadeType = "CASCADE_STORAGE_BANK";
    public const string ShredderType = "SHREDDER";
    public const string MixTankType = "SLURRY_MIX_TANK";
    public const string DigestateType = "DIGESTATE_SEPARATOR";
    public const string CivilType = "CIVIL_WORKS";
    public const string DispatchType = "CBG_DISPATCH";

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
    /// Dilution water and slurry flow are read from the saved slurry mix tank row when it exists.
    /// Older runs without that row fall back to values derived from stored data:
    /// Q = VS(kg/day) / (OLR * HRT), and dilution water = Q * density - total feed (never below 0).
    /// Thermal, upgrading, digestate, civil, pre-treatment and dispatch sections are read from
    /// the saved equipment rows; sections missing from an older run stay at zero.
    /// </summary>
    public async Task<SimulationResultDto?> GetResultDtoAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runId, cancellationToken);
        if (run is null) return null;

        var equipment = await GetEquipmentAsync(runId, cancellationToken);
        var items = equipment.Select(e => new EquipmentSizingDto
        {
            EquipmentTag = e.EquipmentTag,
            EquipmentType = e.EquipmentType,
            Quantity = e.Quantity,
            GeometryParams = ParseObject(e.GeometryParamsJson),
            ProcessDuty = ParseObject(e.ProcessDutyJson),
            UnitCost = e.UnitCost,
            TotalCost = e.TotalCost
        }).ToList();

        var vsKgDay = (double)run.BlendedVsTpd * 1000.0;
        var olr = (double)run.OlrKgVsM3Day;
        var hrt = (double)run.HrtDays;
        var totalFeed = (double)run.TotalFeedTpd;

        var mix = items.FirstOrDefault(e => e.EquipmentType == MixTankType);

        var slurryFlowM3Day = Num(mix?.ProcessDuty, "slurryFlowM3Day")
            ?? (olr > 0 && hrt > 0 ? vsKgDay / (olr * hrt) : 0.0);
        var dilutionWaterTpd = Num(mix?.ProcessDuty, "dilutionWaterTpd")
            ?? Math.Max(0.0, (slurryFlowM3Day * SlurryDensityTPerM3) - totalFeed);
        var dilutionWaterM3Day = Num(mix?.ProcessDuty, "dilutionWaterM3Day") ?? dilutionWaterTpd;

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
                DilutionWaterM3Day = Math.Round(dilutionWaterM3Day, 2),
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
            PreTreatment = BuildPreTreatment(items),
            Thermal = BuildThermal(items),
            Upgrading = BuildUpgrading(items),
            Digestate = BuildDigestate(items),
            Dispatch = BuildDispatch(items),
            EquipmentSizing = items,
            Warnings = ParseStringList(run.WarningsJson)
        };
    }

    // ------------------------------------------------------------------ Section builders

    private static PreTreatmentDto BuildPreTreatment(List<EquipmentSizingDto> items)
    {
        var shredder = items.FirstOrDefault(e => e.EquipmentType == ShredderType);
        var mix = items.FirstOrDefault(e => e.EquipmentType == MixTankType);

        return new PreTreatmentDto
        {
            ShredderCapacityTph = Num(shredder?.ProcessDuty, "capacityTph") ?? 0,
            ShredderPowerKw = Num(shredder?.ProcessDuty, "powerKw") ?? 0,
            MixTankVolumeM3 = Num(mix?.GeometryParams, "volumeM3") ?? 0,
            MixerPowerKw = Num(mix?.ProcessDuty, "mixerPowerKw") ?? 0,
            DilutionWaterM3Day = Num(mix?.ProcessDuty, "dilutionWaterM3Day") ?? 0
        };
    }

    private static ThermalMechanicalDto BuildThermal(List<EquipmentSizingDto> items)
    {
        var digester = items.FirstOrDefault(e => e.EquipmentType == DashboardRepository.DigesterEquipmentType);
        var hx = items.FirstOrDefault(e => e.EquipmentType == HeatExchangerType);

        var tankCount = digester?.Quantity ?? hx?.Quantity ?? 0;
        var dutyPerTank = Num(hx?.ProcessDuty, "thermalDutyKw") ?? 0;

        var specified = NumList(digester?.GeometryParams, "shellThicknessMmArray");
        var calculated = NumList(digester?.GeometryParams, "shellCalculatedMmArray");

        var courses = new List<ShellCourseDto>();
        for (var i = 0; i < specified.Count; i++)
        {
            courses.Add(new ShellCourseDto
            {
                CourseIndex = i + 1,
                CalculatedMm = i < calculated.Count ? calculated[i] : 0,
                SpecifiedMm = specified[i]
            });
        }

        return new ThermalMechanicalDto
        {
            TankCount = tankCount,
            FeedHeatingKwPerTank = Num(hx?.ProcessDuty, "feedHeatingKw") ?? 0,
            TransmissionLossKwPerTank = Num(hx?.ProcessDuty, "transmissionLossKw") ?? 0,
            TotalThermalDutyKwPerTank = dutyPerTank,
            TotalThermalDutyKw = dutyPerTank * Math.Max(tankCount, 0),
            HeatExchangerAreaM2PerTank = Num(hx?.GeometryParams, "areaM2") ?? 0,
            TotalMixingPowerKwPerTank = Num(digester?.ProcessDuty, "mixingPowerKw") ?? 0,
            AgitatorCountPerTank = (int)(Num(digester?.ProcessDuty, "agitatorCount") ?? 0),
            PowerPerAgitatorKw = Num(digester?.ProcessDuty, "powerPerAgitatorKw") ?? 0,
            ShellCourses = courses
        };
    }

    private static UpgradingDto BuildUpgrading(List<EquipmentSizingDto> items)
    {
        var skid = items.FirstOrDefault(e => e.EquipmentType.EndsWith(UpgradingSkidSuffix, StringComparison.Ordinal));
        var compressor = items.FirstOrDefault(e => e.EquipmentType == CompressorType);
        var cascade = items.FirstOrDefault(e => e.EquipmentType == CascadeType);

        var technology = skid is null
            ? string.Empty
            : skid.EquipmentType[..^UpgradingSkidSuffix.Length];

        return new UpgradingDto
        {
            Technology = technology,
            CbgNm3Day = Num(skid?.ProcessDuty, "cbgNm3Day") ?? 0,
            CbgKgDay = Num(skid?.ProcessDuty, "cbgKgDay") ?? 0,
            UpgradingPowerKw = Num(skid?.ProcessDuty, "upgradingPowerKw") ?? 0,
            CompressorPowerKw = Num(compressor?.ProcessDuty, "compressorPowerKw") ?? 0,
            CompressorCapacityNm3Hr = Num(compressor?.ProcessDuty, "capacityNm3Hr") ?? 0,
            CascadeWaterCapacityLiters = Num(cascade?.ProcessDuty, "totalWaterCapacityLiters") ?? 0,
            CascadeBanks3000L = cascade?.Quantity ?? 0
        };
    }

    private static DigestateCivilDto BuildDigestate(List<EquipmentSizingDto> items)
    {
        var digestate = items.FirstOrDefault(e => e.EquipmentType == DigestateType);
        var civil = items.FirstOrDefault(e => e.EquipmentType == CivilType);

        return new DigestateCivilDto
        {
            SolidCakeTpd = Num(digestate?.ProcessDuty, "solidCakeTpd") ?? 0,
            LiquidLfomTpd = Num(digestate?.ProcessDuty, "liquidLfomTpd") ?? 0,
            ExcavationM3 = Num(civil?.ProcessDuty, "excavationM3") ?? 0,
            RccConcreteM3 = Num(civil?.ProcessDuty, "rccConcreteM3") ?? 0,
            SteelRebarTons = Num(civil?.ProcessDuty, "steelRebarTons") ?? 0
        };
    }

    private static CbgDispatchDto BuildDispatch(List<EquipmentSizingDto> items)
    {
        var dispatch = items.FirstOrDefault(e => e.EquipmentType == DispatchType);

        return new CbgDispatchDto
        {
            CbgTonnesPerDay = Num(dispatch?.ProcessDuty, "cbgTonnesPerDay") ?? 0,
            CbgTonnesPerYear = Num(dispatch?.ProcessDuty, "cbgTonnesPerYear") ?? 0,
            TankerPayloadKg = Num(dispatch?.ProcessDuty, "tankerPayloadKg") ?? 0,
            TankerLoadsPerDay = (int)(Num(dispatch?.ProcessDuty, "tankerLoadsPerDay") ?? 0),
            FillTimeHoursPerTanker = Num(dispatch?.ProcessDuty, "fillTimeHoursPerTanker") ?? 0,
            BufferHoursAvailable = Num(dispatch?.ProcessDuty, "bufferHoursAvailable") ?? 0
        };
    }

    // ------------------------------------------------------------------ JSON helpers

    private static double? Num(IDictionary<string, object?>? values, string key) =>
        DashboardRepository.ReadNumber(values, key);

    private static List<double> NumList(IDictionary<string, object?>? values, string key)
    {
        var result = new List<double>();
        if (values is null) return result;

        foreach (var pair in values)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (pair.Value is IEnumerable<object?> list)
            {
                foreach (var item in list)
                {
                    result.Add(item switch
                    {
                        long l => l,
                        int i => i,
                        double d => d,
                        decimal m => (double)m,
                        _ => 0.0
                    });
                }
            }

            break;
        }

        return result;
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