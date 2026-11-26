using System.Text.Json;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Engine;

/// <summary>
/// Turns a CalculationResult into SimulationRun + SizedEquipment rows.
/// Same logic as SimulationController, shared so proposals can run the engine without a controller.
/// </summary>
public static class SimulationRunBuilder
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

    public static List<FeedstockInput> BuildInputs(
        IReadOnlyCollection<ProjectFeedstockMix> mix,
        IEnumerable<Feedstock> feedstocks)
    {
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

    public static SimulationRun BuildRun(Guid projectId, int version, CalculationResult r) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
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

    public static List<SizedEquipment> BuildEquipment(Guid runId, UpgradingTech tech, CalculationResult r)
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
            new Dictionary<string, object?>
            {
                ["volumeM3"] = r.PreTreatment.MixTankVolumeM3,
                ["diameterM"] = r.PreTreatment.MixTankDiameterM,
                ["totalHeightM"] = r.PreTreatment.MixTankHeightM
            },
            new Dictionary<string, object?>
            {
                ["mixerPowerKw"] = r.PreTreatment.MixerPowerKw,
                ["dilutionWaterM3Day"] = r.PreTreatment.DilutionWaterM3Day,
                ["dilutionWaterTpd"] = r.Blending.MWaterTpd,
                ["slurryFlowM3Day"] = r.Blending.QSlurryM3Day
            },
            SlurryMixTankUnitCost));


        list.Add(Equipment(runId, "FT-101", "FEED_TANK", 1,
            new Dictionary<string, object?>
            {
                ["volumeM3"] = r.PreTreatment.FeedTankVolumeM3,
                ["diameterM"] = r.PreTreatment.FeedTankDiameterM,
                ["totalHeightM"] = r.PreTreatment.FeedTankHeightM
            },
            new Dictionary<string, object?>
            {
                ["mixerPowerKw"] = r.PreTreatment.FeedTankMixerPowerKw
            },
            0m));

        list.Add(Equipment(runId, "DT-101", "DIGESTATE_STORAGE_TANK", r.Digester.NumDigestateTanks,
            new Dictionary<string, object?>
            {
                ["volumeM3"] = Math.Round(r.Digester.DigestateStorageVolumeM3 / r.Digester.NumDigestateTanks, 1),
                ["diameterM"] = r.Digester.DigestateTankDiameterM,
                ["totalHeightM"] = r.Digester.DigestateTankHeightM
            },
            new Dictionary<string, object?>(),
            0m));

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
}