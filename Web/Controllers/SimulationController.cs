using System.Text.Json;
using BiogasApp.Business.Engine;
using BiogasApp.Business.Rules;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class SimulationController : Controller
{
    private const decimal DigesterUnitCost = 185000m;
    private const decimal UpgradingSkidUnitCost = 320000m;
    private const decimal HeatExchangerUnitCost = 450000m;
    private const decimal CompressorUnitCost = 4500000m;
    private const decimal CascadeBankUnitCost = 600000m;
    private const decimal ShredderUnitCost = 1200000m;
    private const decimal SlurryMixTankUnitCost = 800000m;
    private const decimal DigestateSeparatorUnitCost = 1500000m;
    private const decimal CbgDispatchUnitCost = 2500000m;
    private const decimal RccRatePerM3 = 8000m;
    private const decimal ExcavationRatePerM3 = 300m;
    private const decimal SteelRatePerTon = 65000m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProjectRepository _projects;
    private readonly IFeedstockRepository _feedstocks;
    private readonly ISimulationRepository _simulations;
    private readonly IDashboardRepository _dashboards;
    private readonly ICalculationEngine _engine;
    private readonly UpgradingService _upgrading;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(
        IProjectRepository projects,
        IFeedstockRepository feedstocks,
        ISimulationRepository simulations,
        IDashboardRepository dashboards,
        ICalculationEngine engine,
        UpgradingService upgrading,
        ILogger<SimulationController> logger)
    {
        _projects = projects;
        _feedstocks = feedstocks;
        _simulations = simulations;
        _dashboards = dashboards;
        _engine = engine;
        _upgrading = upgrading;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Mixer

    [HttpGet]
    public async Task<IActionResult> Mixer(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var mix = await _projects.GetFeedstockMixAsync(projectId, cancellationToken);
        var all = await _feedstocks.GetAllAsync(null, null, cancellationToken);
        var names = all.ToDictionary(f => f.Id, f => f.Name);

        var model = new MixerViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            AvailableFeedstocks = all.Select(ToFeedstockViewModel).ToList(),
            Rows = mix.Select(m => new MixerRowViewModel
            {
                FeedstockId = m.FeedstockId,
                FeedstockName = names.TryGetValue(m.FeedstockId, out var n) ? n : string.Empty,
                DailyTonnageTpd = m.DailyTonnageTpd,
                SeasonalAvailabilityDays = m.SeasonalAvailabilityDays,
                CustomTsOverride = m.CustomTsOverride,
                CustomVsOverride = m.CustomVsOverride
            }).ToList()
        };

        await FillGaugesAsync(model, project, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] MixerViewModel model, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(model.ProjectId, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        await FillGaugesAsync(model, project, cancellationToken);

        return Json(new
        {
            totalFeedTpd = model.TotalFeedTpd,
            blendedTsPercent = model.BlendedTsPercent,
            blendedCnRatio = model.BlendedCnRatio,
            cnGaugeZone = model.CnGaugeZone,
            warnings = model.Warnings
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(MixerViewModel model, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(model.ProjectId, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        model.Rows = (model.Rows ?? new List<MixerRowViewModel>())
            .Where(r => r.FeedstockId != Guid.Empty && r.DailyTonnageTpd > 0)
            .ToList();

        if (!ModelState.IsValid || model.Rows.Count == 0)
        {
            if (model.Rows.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Add at least one feedstock with a tonnage above zero.");
            }

            model.ProjectName = project.Name;
            var all = await _feedstocks.GetAllAsync(null, null, cancellationToken);
            model.AvailableFeedstocks = all.Select(ToFeedstockViewModel).ToList();
            await FillGaugesAsync(model, project, cancellationToken);
            return View(nameof(Mixer), model);
        }

        try
        {
            var mix = model.Rows.Select(r => new ProjectFeedstockMix
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                FeedstockId = r.FeedstockId,
                DailyTonnageTpd = r.DailyTonnageTpd,
                SeasonalAvailabilityDays = r.SeasonalAvailabilityDays <= 0 ? 365 : r.SeasonalAvailabilityDays,
                CustomTsOverride = r.CustomTsOverride,
                CustomVsOverride = r.CustomVsOverride
            }).ToList();

            await _projects.SaveFeedstockMixAsync(project.Id, mix, cancellationToken);

            var inputs = await BuildInputsAsync(mix, cancellationToken);
            var result = _engine.Run(new CalculationInput(
                inputs,
                project.DigesterRegime,
                project.UpgradingTech,
                (double)project.AmbientTempMinC,
                model.TargetSlurryTsPercent));

            var version = await _simulations.GetNextRunVersionAsync(project.Id, cancellationToken);
            var run = BuildRun(project, version, result);
            var equipment = BuildEquipment(run.Id, project.UpgradingTech, result);

            await _simulations.AddRunAsync(run, equipment, cancellationToken);

            if (project.Status == ProjectStatus.Draft)
            {
                await _projects.SetStatusAsync(project.Id, ProjectStatus.Simulated, cancellationToken);
            }

            TempData["Success"] = $"Simulation v{version} completed.";
            return RedirectToAction(nameof(Dashboard), new { runId = run.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation failed for project {ProjectId}", project.Id);
            ModelState.AddModelError(string.Empty, "Simulation failed. Check the inputs and try again.");
        }

        model.ProjectName = project.Name;
        var feedstocks = await _feedstocks.GetAllAsync(null, null, cancellationToken);
        model.AvailableFeedstocks = feedstocks.Select(ToFeedstockViewModel).ToList();
        return View(nameof(Mixer), model);
    }

    // ------------------------------------------------------------------ Results

    [HttpGet]
    public async Task<IActionResult> Dashboard(Guid runId, UpgradingTech? tech, CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }

        var project = await _projects.GetByIdAsync(run.ProjectId, cancellationToken);
        var result = await _simulations.GetResultDtoAsync(runId, cancellationToken);
        var kpis = await _dashboards.GetDashboardAsync(runId, cancellationToken);
        if (project == null || result == null || kpis == null)
        {
            return NotFound();
        }

        kpis.TechnologyComparison = BuildTechnologyComparison(run);

        var model = new SimulationDashboardViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            SimulationRunId = run.Id,
            RunVersion = run.RunVersion,
            ProjectStatus = project.Status,
            SelectedTechnology = tech ?? project.UpgradingTech,
            Kpis = kpis,
            MassBalance = result.MassBalance,
            DigesterKinetics = result.DigesterKinetics,
            PreTreatment = result.PreTreatment,
            Thermal = result.Thermal,
            Upgrading = result.Upgrading,
            Digestate = result.Digestate,
            Dispatch = result.Dispatch,
            Equipment = result.EquipmentSizing,
            Warnings = result.Warnings
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Results(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _simulations.GetResultDtoAsync(runId, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> History(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        var runs = await _simulations.GetRunsByProjectAsync(projectId, cancellationToken);

        ViewData["ProjectId"] = project.Id;
        ViewData["ProjectName"] = project.Name;
        return View(runs.OrderByDescending(r => r.RunVersion).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Compare(Guid runA, Guid runB, CancellationToken cancellationToken)
    {
        var a = await _simulations.GetResultDtoAsync(runA, cancellationToken);
        var b = await _simulations.GetResultDtoAsync(runB, cancellationToken);
        if (a == null || b == null)
        {
            return NotFound();
        }

        return View(new List<SimulationResultDto> { a, b });
    }

    // ------------------------------------------------------------------ Helpers

    private async Task FillGaugesAsync(MixerViewModel model, Project project, CancellationToken cancellationToken)
    {
        model.TotalFeedTpd = 0;
        model.BlendedTsPercent = 0;
        model.BlendedCnRatio = 0;
        model.CnGaugeZone = "Green";
        model.Warnings = new List<string>();

        var rows = (model.Rows ?? new List<MixerRowViewModel>())
            .Where(r => r.FeedstockId != Guid.Empty && r.DailyTonnageTpd > 0)
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        try
        {
            var mix = rows.Select(r => new ProjectFeedstockMix
            {
                FeedstockId = r.FeedstockId,
                DailyTonnageTpd = r.DailyTonnageTpd,
                SeasonalAvailabilityDays = r.SeasonalAvailabilityDays,
                CustomTsOverride = r.CustomTsOverride,
                CustomVsOverride = r.CustomVsOverride
            }).ToList();

            var inputs = await BuildInputsAsync(mix, cancellationToken);
            var result = _engine.Run(new CalculationInput(
                inputs,
                project.DigesterRegime,
                project.UpgradingTech,
                (double)project.AmbientTempMinC,
                model.TargetSlurryTsPercent));

            model.TotalFeedTpd = result.Blending.MFeedTotalTpd;
            model.BlendedTsPercent = result.Blending.BlendedTsPct;
            model.BlendedCnRatio = result.Blending.BlendedCnRatio;
            model.CnGaugeZone = FeedstockWarningRules.GetCnGaugeZone(result.Blending.BlendedCnRatio);
            model.Warnings = result.Warnings.ToList();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            model.Warnings = new List<string> { ex.Message };
        }
    }

    private async Task<List<FeedstockInput>> BuildInputsAsync(
        IReadOnlyCollection<ProjectFeedstockMix> mix,
        CancellationToken cancellationToken)
    {
        var feedstocks = await _feedstocks.GetByIdsAsync(mix.Select(m => m.FeedstockId), cancellationToken);
        var lookup = feedstocks.ToDictionary(f => f.Id);

        var inputs = new List<FeedstockInput>();
        foreach (var m in mix)
        {
            if (!lookup.TryGetValue(m.FeedstockId, out var f))
            {
                throw new InvalidOperationException($"Feedstock {m.FeedstockId} was not found.");
            }

            inputs.Add(new FeedstockInput(
                Name: f.Name,
                Category: f.Category.ToString(),
                MFeedTpd: (double)m.DailyTonnageTpd,
                TsPct: (double)(m.CustomTsOverride ?? f.TsPercent),
                VsPctTs: (double)(m.CustomVsOverride ?? f.VsPercentOfTs),
                CnRatio: (double)f.CnRatio,
                Bmp: (double)f.BmpNm3PerKgVs,
                DegradationFactor: (double)f.DegradationFactor));
        }

        return inputs;
    }

    private static SimulationRun BuildRun(Project project, int version, CalculationResult r) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = project.Id,
        RunVersion = version,
        TotalFeedTpd = (decimal)r.Blending.MFeedTotalTpd,
        BlendedTsPercent = (decimal)r.Blending.BlendedTsPct,
        BlendedVsTpd = (decimal)(r.Blending.VsTotalKgDay / 1000.0),
        BlendedCnRatio = (decimal)r.Blending.BlendedCnRatio,
        HrtDays = (decimal)r.Digester.ActualHrtDays,
        OlrKgVsM3Day = (decimal)r.Digester.ActualOlr,
        RawBiogasNm3Day = (decimal)r.Gas.QBiogasNm3Day,
        Ch4Percent = (decimal)r.Gas.Ch4Pct,
        Co2Percent = (decimal)r.Gas.Co2Pct,
        H2sPpm = (decimal)r.Gas.H2sPpm,
        NetCbgKgDay = (decimal)r.Upgrading.MCbgKgDay,
        ParasiticHeatKw = (decimal)r.ParasiticHeatKw,
        ParasiticPowerKw = (decimal)r.ParasiticPowerKw,
        WarningsJson = JsonSerializer.Serialize(r.Warnings, JsonOptions),
        CreatedAt = DateTime.UtcNow
    };

    private static List<SizedEquipment> BuildEquipment(Guid runId, UpgradingTech tech, CalculationResult r)
    {
        var list = new List<SizedEquipment>();

        var digesterGeometry = new Dictionary<string, object?>
        {
            ["diameterM"] = r.Digester.DiameterM,
            ["liquidHeightM"] = r.Digester.LiquidHeightM,
            ["totalHeightM"] = r.Digester.TotalHeightM,
            ["shellThicknessMmArray"] = r.Mechanical.ShellCourses.Select(c => c.SpecifiedMm).ToArray(),
            ["shellCalculatedMmArray"] = r.Mechanical.ShellCourses.Select(c => c.CalculatedMm).ToArray(),
            ["domeHeightM"] = Math.Round(r.Digester.DiameterM * 0.15, 1)
        };
        var digesterDuty = new Dictionary<string, object?>
        {
            ["workingVolumeM3"] = Math.Round(r.Digester.VWorkingPerTankM3, 1),
            ["mixingPowerKw"] = r.Mechanical.TotalMixingPowerKw,
            ["agitatorCount"] = r.Mechanical.AgitatorCount,
            ["powerPerAgitatorKw"] = r.Mechanical.PowerPerAgitatorKw,
            ["heatingThermalKw"] = r.Heat.QTotalThermalDutyKw
        };
        list.Add(Equipment(runId, "D-101", "PRIMARY_DIGESTER_CSTR", r.Digester.NumTanks,
            digesterGeometry, digesterDuty, DigesterUnitCost));

        list.Add(Equipment(runId, "HX-101", "HEAT_EXCHANGER", r.Digester.NumTanks,
            new Dictionary<string, object?> { ["areaM2"] = r.Heat.HeatExchangerAreaM2 },
            new Dictionary<string, object?>
            {
                ["thermalDutyKw"] = r.Heat.QTotalThermalDutyKw,
                ["feedHeatingKw"] = r.Heat.QFeedHeatingKw,
                ["transmissionLossKw"] = r.Heat.QTransmissionLossKw
            },
            HeatExchangerUnitCost));
        

        list.Add(Equipment(runId, "UPG-101", $"{tech.ToString().ToUpperInvariant()}_UPGRADING_SKID", 1,
            new Dictionary<string, object?>
            {
                ["skidLengthM"] = 12.0,
                ["skidWidthM"] = 3.5,
                ["skidHeightM"] = 3.0
            },
            new Dictionary<string, object?>
            {
                ["rawBiogasInletNm3Hr"] = Math.Round(r.Gas.QBiogasNm3Hr, 1),
                ["upgradingPowerKw"] = r.Upgrading.UpgradingPowerKw,
                ["cbgNm3Day"] = r.Upgrading.QCbgNm3Day,
                ["cbgKgDay"] = r.Upgrading.MCbgKgDay
            },
            UpgradingSkidUnitCost));

        list.Add(Equipment(runId, "CMP-101", "HIGH_PRESSURE_COMPRESSOR", 1,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["compressorPowerKw"] = r.Upgrading.CompressorPowerKw,
                ["capacityNm3Hr"] = r.Upgrading.CompressorCapacityNm3Hr
            },
            CompressorUnitCost));

        list.Add(Equipment(runId, "CSC-101", "CASCADE_STORAGE_BANK", Math.Max(r.Upgrading.NumCascadeBanks3000L, 1),
            new Dictionary<string, object?> { ["waterCapacityLitersPerBank"] = 3000 },
                       new Dictionary<string, object?> { ["totalWaterCapacityLiters"] = r.Upgrading.CascadeWaterCapacityLiters },
            CascadeBankUnitCost));

        // Stage 2 pre-treatment
        list.Add(Equipment(runId, "PRE-101", "SHREDDER", 1,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["capacityTph"] = r.PreTreatment.ShredderCapacityTph,
                ["powerKw"] = r.PreTreatment.ShredderPowerKw
            },
            ShredderUnitCost));

        list.Add(Equipment(runId, "MIX-101", "SLURRY_MIX_TANK", 1,
            new Dictionary<string, object?> { ["volumeM3"] = r.PreTreatment.MixTankVolumeM3 },
            new Dictionary<string, object?>
            {
                ["mixerPowerKw"] = r.PreTreatment.MixerPowerKw,
                ["dilutionWaterM3Day"] = r.PreTreatment.DilutionWaterM3Day,
                ["dilutionWaterTpd"] = r.Blending.MWaterTpd,
                ["slurryFlowM3Day"] = r.Blending.QSlurryM3Day
            },
            SlurryMixTankUnitCost));
        

        // Stage 6 digestate, civil and dispatch
        list.Add(Equipment(runId, "DIG-101", "DIGESTATE_SEPARATOR", 1,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["solidCakeTpd"] = r.Bom.DigestateSolidCakeTpd,
                ["liquidLfomTpd"] = r.Bom.DigestateLiquidLfomTpd
            },
            DigestateSeparatorUnitCost));

        list.Add(Equipment(runId, "CIV-101", "CIVIL_WORKS", 1,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["excavationM3"] = r.Bom.CivilExcavationM3,
                ["rccConcreteM3"] = r.Bom.CivilRccConcreteM3,
                ["steelRebarTons"] = r.Bom.StructuralSteelRebarTons
            },
            (decimal)r.Bom.CivilRccConcreteM3 * RccRatePerM3 +
            (decimal)r.Bom.CivilExcavationM3 * ExcavationRatePerM3 +
            (decimal)r.Bom.StructuralSteelRebarTons * SteelRatePerTon));

        list.Add(Equipment(runId, "DSP-101", "CBG_DISPATCH", 1,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["cbgTonnesPerDay"] = r.Dispatch.CbgTonnesPerDay,
                ["cbgTonnesPerYear"] = r.Dispatch.CbgTonnesPerYear,
                ["tankerPayloadKg"] = r.Dispatch.TankerPayloadKg,
                ["tankerLoadsPerDay"] = r.Dispatch.TankerLoadsPerDay,
                ["fillTimeHoursPerTanker"] = r.Dispatch.FillTimeHoursPerTanker,
                ["bufferHoursAvailable"] = r.Dispatch.BufferHoursAvailable
            },
            CbgDispatchUnitCost));

        return list;
    }

    private static SizedEquipment Equipment(
        Guid runId,
        string tag,
        string type,
        int quantity,
        Dictionary<string, object?> geometry,
        Dictionary<string, object?> duty,
        decimal unitCost) => new()
        {
            Id = Guid.NewGuid(),
            SimulationRunId = runId,
            EquipmentTag = tag,
            EquipmentType = type,
            Quantity = quantity,
            GeometryParamsJson = JsonSerializer.Serialize(geometry, JsonOptions),
            ProcessDutyJson = JsonSerializer.Serialize(duty, JsonOptions),
            UnitCost = unitCost,
            TotalCost = unitCost * quantity
        };

    private List<TechnologyComparisonDto> BuildTechnologyComparison(SimulationRun run)
    {
        var list = new List<TechnologyComparisonDto>();
        foreach (var tech in Enum.GetValues<UpgradingTech>())
        {
            var u = _upgrading.Calculate((double)run.RawBiogasNm3Day, (double)run.Ch4Percent, tech);
            var spec = BiogasApp.Business.Constants.UpgradingSpecs.Get(tech);

            list.Add(new TechnologyComparisonDto
            {
                Technology = tech.ToString().ToUpperInvariant(),
                RecoveryPercent = spec.Recovery * 100.0,
                PurityPercent = spec.Purity * 100.0,
                UpgradingPowerKw = u.UpgradingPowerKw,
                CompressorPowerKw = u.CompressorPowerKw,
                NetCbgKgDay = u.MCbgKgDay
            });
        }

        return list;
    }

    private static FeedstockViewModel ToFeedstockViewModel(Feedstock f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Category = f.Category,
        TsPercent = f.TsPercent,
        VsPercentOfTs = f.VsPercentOfTs,
        CnRatio = f.CnRatio,
        BmpNm3PerKgVs = f.BmpNm3PerKgVs,
        BulkDensityKgM3 = f.BulkDensityKgM3,
        DegradationFactor = f.DegradationFactor,
        DefaultCostPerTon = f.DefaultCostPerTon
    };
}