using System.Text.Json;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Pid;

public class PidTemplateBuilder
{
    public const int StageFeed = 1;
    public const int StagePreTreatment = 2;
    public const int StageDigestion = 3;
    public const int StageGasHolder = 4;
    public const int StageUpgrading = 5;
    public const int StageCbg = 6;

    private static readonly decimal[] StandardDn = { 25, 40, 50, 80, 100, 150, 200, 250, 300, 400, 500 };

    private readonly PidTagService _tags;

    public PidTemplateBuilder(PidTagService tags)
    {
        _tags = tags;
    }

    public PidDiagram Build(
        Guid projectId,
        Guid simulationRunId,
        IReadOnlyList<ProjectEquipmentItem> items,
        IReadOnlyList<SizedEquipment> sized,
        string? createdBy = null)
    {
        var d = new PidDiagram
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SimulationRunId = simulationRunId,
            DrawingNo = "PID-001",
            Title = "P&ID - Process Overview",
            RevisionNo = 1,
            Status = PidApprovalStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        var enabled = items.Where(i => i.IsEnabled).ToList();

        ProjectEquipmentItem? Pick(string prefix) => enabled
            .Where(i => PidTagService.PrefixOf(i.Tag).Equals(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Tag, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        SizedEquipment? Sz(string tag) => sized
            .FirstOrDefault(s => s.EquipmentTag.Equals(tag, StringComparison.OrdinalIgnoreCase));

        string Label(ProjectEquipmentItem i) => i.Quantity > 1 ? $"{i.Name} (x{i.Quantity})" : i.Name;

        PidEquipmentNode Add(string tag, string name, PidSymbolType sym, int stage, Guid? itemId, Guid? sizedId)
        {
            var node = new PidEquipmentNode
            {
                Id = Guid.NewGuid(),
                PidDiagramId = d.Id,
                EquipmentItemId = itemId,
                SizedEquipmentId = sizedId,
                Tag = tag,
                Name = name,
                SymbolType = sym,
                StageNo = stage
            };
            d.Nodes.Add(node);
            return node;
        }

        PidEquipmentNode AddPump(string name, int stage)
            => Add(_tags.NextPump(d.Nodes.Select(x => x.Tag)), name, PidSymbolType.Pump, stage, null, null);

        void Link(PidEquipmentNode a, PidEquipmentNode b, PidMedium medium, decimal dn)
        {
            d.Lines.Add(new PidLine
            {
                Id = Guid.NewGuid(),
                PidDiagramId = d.Id,
                LineNo = _tags.NextLineNo(medium, dn, d.Lines.Select(x => x.LineNo)),
                FromNodeId = a.Id,
                ToNodeId = b.Id,
                Medium = medium,
                NominalSizeMm = dn,
                Material = MaterialFor(medium),
                IsInsulated = medium == PidMedium.Slurry
            });
        }

        void Chain(PidMedium medium, decimal dn, params PidEquipmentNode?[] nodes)
        {
            var list = nodes.Where(x => x != null).Select(x => x!).ToList();
            for (var k = 0; k < list.Count - 1; k++)
                Link(list[k], list[k + 1], medium, dn);
        }

        PidValve AddValve(PidLine line, PidValveType type, decimal position)
        {
            var valve = new PidValve
            {
                Id = Guid.NewGuid(),
                PidDiagramId = d.Id,
                PidLineId = line.Id,
                Tag = _tags.NextValve(d.Valves.Select(x => x.Tag)),
                ValveType = type,
                NominalSizeMm = line.NominalSizeMm,
                PositionOnLine = position
            };
            d.Valves.Add(valve);
            return valve;
        }

        PidInstrument AddInstrument(
            PidInstrumentType type, string service, string unit, decimal min, decimal max,
            PidEquipmentNode? node, PidLine? line)
        {
            var ins = new PidInstrument
            {
                Id = Guid.NewGuid(),
                PidDiagramId = d.Id,
                PidEquipmentNodeId = node?.Id,
                PidLineId = line?.Id,
                Tag = _tags.NextInstrument(type, d.Instruments.Select(x => x.Tag)),
                InstrumentType = type,
                Service = service,
                Unit = unit,
                RangeMin = min,
                RangeMax = max
            };
            d.Instruments.Add(ins);
            return ins;
        }

        var ftI = Pick("FT");
        var mtI = Pick("MT");
        var adI = Pick("AD");
        var dtI = Pick("DT");
        var slsI = Pick("SLS");
        var suI = Pick("SU");
        var cfI = Pick("CF");
        var flI = Pick("FL");

        var mixS = Sz("MIX-101");
        var preS = Sz("PRE-101");
        var hxS = Sz("HX-101");
        var digS = Sz("D-101");
        var upgS = Sz("UPG-101");
        var cmpS = Sz("CMP-101");
        var cscS = Sz("CSC-101");
        var ftS = Sz("FT-101");
        var dtS = Sz("DT-101");

        var slurryDn = DnFor(ReadDuty(mixS, "slurryFlowM3Day") / 24.0, 1.5, 100m);
        var biogasDn = DnFor(ReadDuty(upgS, "rawBiogasInletNm3Hr"), 10.0, 100m);
        var cbgDn = DnFor(ReadDuty(upgS, "cbgNm3Day") / 24.0, 15.0, 25m);

        PidEquipmentNode? ft = ftI == null ? null
            : Add(ftI.Tag, Label(ftI), PidSymbolType.Tank, StageFeed, ftI.Id, ftS?.Id);

        PidEquipmentNode? pre = preS == null ? null
            : Add(preS.EquipmentTag, "Shredder", PidSymbolType.Vessel, StagePreTreatment, null, preS.Id);

        PidEquipmentNode? mt = mtI == null ? null
            : Add(mtI.Tag, Label(mtI), PidSymbolType.Tank, StagePreTreatment, mtI.Id, mixS?.Id);

        PidEquipmentNode? p101 = (mtI != null && adI != null)
            ? AddPump("Digester feed pump", StagePreTreatment) : null;

        PidEquipmentNode? ad = adI == null ? null
            : Add(adI.Tag, Label(adI), PidSymbolType.Digester, StageDigestion, adI.Id, digS?.Id);

        PidEquipmentNode? hx = hxS == null ? null
            : Add(hxS.EquipmentTag, "Heat exchanger", PidSymbolType.Vessel, StageDigestion, null, hxS.Id);

        PidEquipmentNode? p102 = (adI != null && dtI != null)
            ? AddPump("Digestate pump", StageDigestion) : null;

        PidEquipmentNode? dt = dtI == null ? null
            : Add(dtI.Tag, Label(dtI), PidSymbolType.Tank, StageDigestion, dtI.Id, dtS?.Id);

        PidEquipmentNode? p103 = (dtI != null && slsI != null)
            ? AddPump("Digestate transfer pump", StageDigestion) : null;

        PidEquipmentNode? sls = slsI == null ? null
            : Add(slsI.Tag, Label(slsI), PidSymbolType.Vessel, StageDigestion, slsI.Id, null);

        PidEquipmentNode? gh = ad == null ? null
            : Add("GH-01", "Gas holder", PidSymbolType.GasHolder, StageGasHolder, null, null);

        PidEquipmentNode? fl = flI == null ? null
            : Add(flI.Tag, Label(flI), PidSymbolType.Flare, StageGasHolder, flI.Id, null);

        PidEquipmentNode? su = suI == null ? null
            : Add(suI.Tag, Label(suI), PidSymbolType.Vessel, StageUpgrading, suI.Id, upgS?.Id);

        PidEquipmentNode? cf = cfI == null ? null
            : Add(cfI.Tag, Label(cfI), PidSymbolType.Blower, StageCbg, cfI.Id, cmpS?.Id);

        PidEquipmentNode? csc = cscS == null ? null
            : Add(cscS.EquipmentTag, "Cascade storage bank", PidSymbolType.Tank, StageCbg, null, cscS.Id);

        Chain(PidMedium.Slurry, slurryDn, ft, pre, mt, p101, hx, ad);
        Chain(PidMedium.Digestate, slurryDn, ad, p102, dt, p103, sls);
        Chain(PidMedium.Biogas, biogasDn, ad, gh, su);
        Chain(PidMedium.Biogas, biogasDn, gh, fl);
        Chain(PidMedium.Cbg, cbgDn, su, cf, csc);

        foreach (var pump in d.Nodes.Where(x => x.SymbolType == PidSymbolType.Pump).ToList())
        {
            foreach (var inLine in d.Lines.Where(x => x.ToNodeId == pump.Id).ToList())
                AddValve(inLine, PidValveType.Gate, 0.7m);

            foreach (var outLine in d.Lines.Where(x => x.FromNodeId == pump.Id).ToList())
            {
                AddValve(outLine, PidValveType.Check, 0.3m);
                AddValve(outLine, PidValveType.Gate, 0.7m);
            }
        }

        if (ad != null)
        {
            AddInstrument(PidInstrumentType.LT, "Digester level", "%", 0m, 100m, ad, null);
            AddInstrument(PidInstrumentType.TT, "Digester temperature", "degC", 0m, 80m, ad, null);
        }

        if (gh != null)
            AddInstrument(PidInstrumentType.PT, "Gas holder pressure", "mbar", 0m, 50m, gh, null);

        if (p101 != null)
        {
            var feedLine = d.Lines.FirstOrDefault(x => x.FromNodeId == p101.Id);
            if (feedLine != null)
                AddInstrument(PidInstrumentType.FT, "Digester feed flow", "m3/h", 0m, 50m, null, feedLine);
        }

        if (gh != null && su != null)
        {
            var gasLine = d.Lines.FirstOrDefault(x => x.FromNodeId == gh.Id && x.ToNodeId == su.Id);
            if (gasLine != null)
                AddInstrument(PidInstrumentType.FT, "Raw biogas flow", "Nm3/h", 0m, 2000m, null, gasLine);
        }

        return d;
    }

    private static string MaterialFor(PidMedium medium) => medium switch
    {
        PidMedium.Biogas => "SS304",
        PidMedium.Cbg => "SS316",
        _ => "CS"
    };

    private static decimal DnFor(double? flowM3h, double velocityMs, decimal fallback)
    {
        if (!flowM3h.HasValue || flowM3h.Value <= 0) return fallback;
        var q = flowM3h.Value / 3600.0;
        var dMm = Math.Sqrt(4.0 * q / (Math.PI * velocityMs)) * 1000.0;
        foreach (var dn in StandardDn)
            if ((double)dn >= dMm) return dn;
        return StandardDn[^1];
    }

    private static double? ReadDuty(SizedEquipment? equipment, string key)
    {
        if (equipment == null || string.IsNullOrWhiteSpace(equipment.ProcessDutyJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(equipment.ProcessDutyJson);
            if (doc.RootElement.TryGetProperty(key, out var p)
                && p.ValueKind == JsonValueKind.Number
                && p.TryGetDouble(out var v))
                return v;
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
