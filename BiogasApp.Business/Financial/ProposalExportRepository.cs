using System.Globalization;
using System.Text;
using BiogasApp.Business.Engine;
using BiogasApp.Business.Financial;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using BiogasApp.Repositories.Repositories;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace BiogasApp.Business.Financial;

/// <summary>
/// Operating-cost assumptions used for the proposal. These are PLACEHOLDER defaults:
/// register your own instance (AddSingleton) or edit the values below.
/// </summary>
public class ProposalAssumptions
{
    public double OmPercentOfCapexPerYear { get; set; } = 3.0;      // maintenance, insurance
    public double ElectricityTariffPerKwh { get; set; } = 8.0;      // INR / kWh
    public double LabourAndOverheadPerYear { get; set; } = 2_400_000; // INR / year
}

/// <summary>
/// Builds the BOM workbook, the proposal PDF and the financial metrics for a simulation run.
/// Lives in the Business project because it needs FinancialModel and DigestateBomService
/// (Repositories cannot reference Business).
///
/// RATES: unit rates come ONLY from the rate map the caller supplies (rateOverrides /
/// request.CustomRateCardOverrides). The caller should pass the tenant's rate-card map
/// (IRateCardRepository.GetRateMapAsync) merged with any user overrides.
///
/// CAPEX = sum of sized_equipment.TotalCost + civil lines (excavation, RCC, rebar) x rates.
/// A civil rate that is missing is costed at 0 and flagged in the workbook.
///
/// Register:  services.AddScoped&lt;IProposalExport, ProposalExportRepository&gt;();
///            services.AddScoped&lt;DigestateBomService&gt;();
/// </summary>
public class ProposalExportRepository : IProposalExport
{
    // Rate-card item codes this class looks up for the civil lines.
    public const string ExcavationItemCode = "CIV-EXC-01";   // per m3
    public const string ConcreteItemCode = "CIV-CONC-01";    // per m3
    public const string RebarItemCode = "CIV-STL-01";        // per kg

    private const string ArtifactDownloadRoute = "/Cad/Download/";

    private readonly ISimulationRepository _simulations;
    private readonly IProjectRepository _projects;
    private readonly IArtifactStorage _storage;
    private readonly ApplicationDbContext _db;
    private readonly DigestateBomService _bom;
    private readonly ProposalAssumptions _assumptions;
    private readonly ILogger<ProposalExportRepository> _logger;

    public ProposalExportRepository(
        ISimulationRepository simulations,
        IProjectRepository projects,
        IArtifactStorage storage,
        ApplicationDbContext db,
        DigestateBomService bom,
        ILogger<ProposalExportRepository> logger,
        ProposalAssumptions? assumptions = null)
    {
        _simulations = simulations;
        _projects = projects;
        _storage = storage;
        _db = db;
        _bom = bom;
        _logger = logger;
        _assumptions = assumptions ?? new ProposalAssumptions();
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Computes metrics, builds BOM.xlsx and Proposal.pdf, stores both and records them as artifacts.</summary>
    /// <exception cref="KeyNotFoundException">Run or project not found.</exception>
    /// <exception cref="ArgumentException">A financial input is out of range.</exception>
    public async Task<ProposalResponseDto> ExportProposalAsync(
        ProposalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(request.FinancialInputs);

        var rates = ToRateMap(request.CustomRateCardOverrides.ToDictionary(
            o => o.ItemCode, o => o.OverrideUnitRate, StringComparer.OrdinalIgnoreCase));

        var data = await LoadDataAsync(request.SimulationRunId, rates, cancellationToken);
        var metrics = await EvaluateAsync(data, request.FinancialInputs, cancellationToken);

        var shortId = data.Run.Id.ToString("N")[..8];
        var excelName = $"BOM_{shortId}_v{data.Run.RunVersion}.xlsx";
        var pdfName = $"Proposal_{shortId}_v{data.Run.RunVersion}.pdf";

        var excelId = await SaveArtifactAsync(data.Run.Id, ArtifactType.BomXlsx, excelName, BuildExcel(data), cancellationToken);
        var pdfId = await SaveArtifactAsync(data.Run.Id, ArtifactType.ProposalPdf, pdfName, BuildPdf(data, metrics), cancellationToken);

        return new ProposalResponseDto
        {
            ProposalId = pdfId,
            FinancialMetrics = metrics,
            ExcelBomUrl = $"{ArtifactDownloadRoute}{excelId}",
            ProposalPdfUrl = $"{ArtifactDownloadRoute}{pdfId}"
        };
    }

    public async Task<byte[]> BuildBomExcelAsync(
        Guid simulationRunId,
        IReadOnlyDictionary<string, decimal>? rateOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(simulationRunId, ToRateMap(rateOverrides), cancellationToken);
        return BuildExcel(data);
    }

    public async Task<byte[]> BuildProposalPdfAsync(
        Guid simulationRunId,
        FinancialMetricsDto metrics,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(simulationRunId, ToRateMap(null), cancellationToken);
        return BuildPdf(data, metrics);
    }

    // ------------------------------------------------------------------ data loading

    private sealed record EquipmentLine(string Tag, string Type, int Quantity, decimal UnitCost, decimal TotalCost);

    private sealed record CivilLine(
        string ItemCode, string Description, string Unit, double Quantity, decimal Rate, bool RateMissing)
    {
        public decimal Amount => (decimal)Quantity * Rate;
    }

    private sealed record ProposalData(
        SimulationRun Run,
        SimulationResultDto Result,
        Project Project,
        List<EquipmentLine> Equipment,
        List<CivilLine> Civil,
        double SolidCakeTpd)
    {
        public decimal EquipmentCapex => Equipment.Sum(e => e.TotalCost);
        public decimal CivilCapex => Civil.Sum(c => c.Amount);
        public decimal TotalCapex => EquipmentCapex + CivilCapex;
    }

    private async Task<ProposalData> LoadDataAsync(
        Guid runId,
        IReadOnlyDictionary<string, decimal> rates,
        CancellationToken cancellationToken)
    {
        var run = await _simulations.GetRunAsync(runId, cancellationToken)
            ?? throw new KeyNotFoundException($"Simulation run {runId} was not found.");

        var result = await _simulations.GetResultDtoAsync(runId, cancellationToken)
            ?? throw new KeyNotFoundException($"Simulation run {runId} was not found.");

        var project = await _projects.GetByIdAsync(run.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {run.ProjectId} was not found.");

        var equipment = result.EquipmentSizing
            .Select(e => new EquipmentLine(e.EquipmentTag, e.EquipmentType, e.Quantity, e.UnitCost, e.TotalCost))
            .ToList();

        // Civil quantities come from the digester geometry saved with the run.
        double excavation = 0, concrete = 0, rebarTons = 0, solidCake = 0;

        var digester = result.EquipmentSizing.FirstOrDefault(
            e => e.EquipmentType == DashboardRepository.DigesterEquipmentType);

        var diameter = DashboardRepository.ReadNumber(digester?.GeometryParams, "diameterM");
        var height = DashboardRepository.ReadNumber(digester?.GeometryParams, "totalHeightM");

        if (digester is not null && diameter > 0 && height > 0)
        {
            var civil = _bom.Calculate(
                result.MassBalance.TotalSlurryFlowM3Day,
                result.MassBalance.BlendedTsPercent,
                diameter.Value,
                height.Value,
                Math.Max(1, digester.Quantity));

            excavation = civil.CivilExcavationM3;
            concrete = civil.CivilRccConcreteM3;
            rebarTons = civil.StructuralSteelRebarTons;
            solidCake = civil.DigestateSolidCakeTpd;
        }
        else
        {
            _logger.LogWarning(
                "Run {RunId}: no '{Type}' equipment with diameterM/totalHeightM found; civil quantities are 0.",
                runId, DashboardRepository.DigesterEquipmentType);
        }

        var civilLines = new List<CivilLine>
        {
            CivilLineOf(ExcavationItemCode, "Excavation in all soil types", "m3", excavation, rates),
            CivilLineOf(ConcreteItemCode, "RCC concrete for digester raft and walls", "m3", concrete, rates),
            CivilLineOf(RebarItemCode, "Reinforcement steel", "kg", rebarTons * 1000.0, rates)
        };

        return new ProposalData(run, result, project, equipment, civilLines, solidCake);
    }

    private static CivilLine CivilLineOf(
        string itemCode, string description, string unit, double quantity,
        IReadOnlyDictionary<string, decimal> rates)
    {
        var found = rates.TryGetValue(itemCode, out var rate);
        return new CivilLine(itemCode, description, unit, quantity, found ? rate : 0m, RateMissing: !found);
    }

    private static IReadOnlyDictionary<string, decimal> ToRateMap(IReadOnlyDictionary<string, decimal>? source)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (source is null) return map;

        foreach (var pair in source)
            map[pair.Key.Trim()] = pair.Value;

        return map;
    }

    // ------------------------------------------------------------------ financials

    private async Task<FinancialMetricsDto> EvaluateAsync(
        ProposalData data,
        FinancialInputsDto input,
        CancellationToken cancellationToken)
    {
        var capex = (double)data.TotalCapex;
        var days = CashFlowBuilder.DefaultOperatingDays;

        var mix = await _projects.GetFeedstockMixAsync(data.Project.Id, cancellationToken);

        var feedstockCost = mix.Sum(m =>
        (double)m.DailyTonnageTpd *
        (double)(m.Feedstock?.DefaultCostPerTon ?? 0m) *
        Math.Min(m.SeasonalAvailabilityDays <= 0 ? days : m.SeasonalAvailabilityDays, days));

        var electricityCost = (double)data.Run.ParasiticPowerKw * 24.0 * days * _assumptions.ElectricityTariffPerKwh;
        var maintenance = capex * _assumptions.OmPercentOfCapexPerYear / 100.0;
        var opex = feedstockCost + electricityCost + maintenance + _assumptions.LabourAndOverheadPerYear;

        var result = FinancialModel.Evaluate(
            new FinancialInputs(
                input.CbgSellingPricePerKg,
                input.FomSellingPricePerTon,
                input.DiscountRatePercent,
                input.ProjectLifeYears,
                input.DebtRatioPercent,
                input.InterestRatePercent),
            capexTotal: capex,
            opexAnnual: opex,
            netCbgKgDay: (double)data.Run.NetCbgKgDay,
            fomTpd: data.SolidCakeTpd);

        return new FinancialMetricsDto
        {
            CapexTotal = Math.Round(result.CapexTotal, 2),
            OpexAnnual = Math.Round(result.OpexAnnual, 2),
            AnnualRevenue = Math.Round(result.AnnualRevenue, 2),
            Npv = Math.Round(result.Npv, 2),
            IrrPercent = result.IrrPercent.HasValue ? Math.Round(result.IrrPercent.Value, 2) : null,
            PaybackPeriodYears = result.PaybackPeriodYears.HasValue ? Math.Round(result.PaybackPeriodYears.Value, 2) : null,
            MinDscr = result.MinDscr.HasValue ? Math.Round(result.MinDscr.Value, 2) : null
        };
    }

    private static void ValidateInputs(FinancialInputsDto input)
    {
        if (input.CbgSellingPricePerKg < 0 || input.FomSellingPricePerTon < 0)
            throw new ArgumentException("Selling prices cannot be negative.", nameof(input));

        if (input.ProjectLifeYears is < 1 or > 50)
            throw new ArgumentException("Project life must be between 1 and 50 years.", nameof(input));

        if (input.DiscountRatePercent is < 0 or > 100 ||
            input.DebtRatioPercent is < 0 or > 100 ||
            input.InterestRatePercent is < 0 or > 100)
            throw new ArgumentException("Discount, debt and interest rates must be between 0 and 100 percent.", nameof(input));
    }

    // ------------------------------------------------------------------ Excel

    private static byte[] BuildExcel(ProposalData data)
    {
        const string money = "#,##0.00";

        using var workbook = new XLWorkbook();

        // Equipment
        var eq = workbook.Worksheets.Add("Equipment");
        WriteHeader(eq, "Tag", "Type", "Qty", "Unit Cost", "Total Cost");

        var row = 2;
        foreach (var e in data.Equipment)
        {
            eq.Cell(row, 1).Value = e.Tag;
            eq.Cell(row, 2).Value = e.Type;
            eq.Cell(row, 3).Value = e.Quantity;
            eq.Cell(row, 4).Value = e.UnitCost;
            eq.Cell(row, 5).Value = e.TotalCost;
            row++;
        }

        eq.Cell(row, 4).Value = "Total";
        eq.Cell(row, 5).Value = data.EquipmentCapex;
        eq.Row(row).Style.Font.Bold = true;
        eq.Range(2, 4, row, 5).Style.NumberFormat.Format = money;
        eq.Columns().AdjustToContents();

        // Civil
        var civil = workbook.Worksheets.Add("Civil");
        WriteHeader(civil, "Item Code", "Description", "Unit", "Quantity", "Rate", "Amount", "Note");

        row = 2;
        foreach (var c in data.Civil)
        {
            civil.Cell(row, 1).Value = c.ItemCode;
            civil.Cell(row, 2).Value = c.Description;
            civil.Cell(row, 3).Value = c.Unit;
            civil.Cell(row, 4).Value = c.Quantity;
            civil.Cell(row, 5).Value = c.Rate;
            civil.Cell(row, 6).Value = c.Amount;
            civil.Cell(row, 7).Value = c.RateMissing ? "Rate missing - costed at 0" : string.Empty;
            row++;
        }

        civil.Cell(row, 5).Value = "Total";
        civil.Cell(row, 6).Value = data.CivilCapex;
        civil.Row(row).Style.Font.Bold = true;
        civil.Range(2, 4, row, 6).Style.NumberFormat.Format = money;
        civil.Columns().AdjustToContents();

        // Summary (first tab)
        var summary = workbook.Worksheets.Add("Summary");
        summary.Position = 1;
        summary.Cell(1, 1).Value = "BOM Summary";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 14;

        summary.Cell(3, 1).Value = "Project";
        summary.Cell(3, 2).Value = data.Project.Name;
        summary.Cell(4, 1).Value = "Simulation version";
        summary.Cell(4, 2).Value = data.Run.RunVersion;
        summary.Cell(5, 1).Value = "Generated (UTC)";
        summary.Cell(5, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        summary.Cell(7, 1).Value = "Equipment CAPEX";
        summary.Cell(7, 2).Value = data.EquipmentCapex;
        summary.Cell(8, 1).Value = "Civil CAPEX";
        summary.Cell(8, 2).Value = data.CivilCapex;
        summary.Cell(9, 1).Value = "Total CAPEX";
        summary.Cell(9, 2).Value = data.TotalCapex;
        summary.Range(9, 1, 9, 2).Style.Font.Bold = true;
        summary.Range(7, 2, 9, 2).Style.NumberFormat.Format = money;

        var missing = data.Civil.Where(c => c.RateMissing).Select(c => c.ItemCode).ToList();
        if (missing.Count > 0)
        {
            summary.Cell(11, 1).Value = "Warning";
            summary.Cell(11, 2).Value = $"Missing rate(s) costed at 0: {string.Join(", ", missing)}";
            summary.Row(11).Style.Font.Bold = true;
        }

        summary.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var range = sheet.Range(1, 1, 1, headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    // ------------------------------------------------------------------ PDF

    private static byte[] BuildPdf(ProposalData data, FinancialMetricsDto metrics)
    {
        var inv = CultureInfo.InvariantCulture;
        string N(double v) => v.ToString("N2", inv);
        string Opt(double? v) => v.HasValue ? N(v.Value) : "n/a";
        string Pretty(string s) => inv.TextInfo.ToTitleCase(s.Replace('_', ' ').ToLowerInvariant());

        var run = data.Run;
        var pdf = new ProposalPdf();

        pdf.Banner("BiogasApp", "Techno-Economic Proposal - Biogas / Bio-CNG Plant");

        pdf.InfoRows(new (string, string)[]
        {
            ("Project", data.Project.Name),
            ("Location", data.Project.LocationCountry),
            ("Simulation version", $"{run.RunVersion}"),
            ("Generated (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", inv))
        });

        pdf.MetricBoxes(new (string, string)[]
        {
            ("Total CAPEX (INR)", N(metrics.CapexTotal)),
            ("Annual revenue (INR)", N(metrics.AnnualRevenue)),
            ("IRR (%)", Opt(metrics.IrrPercent)),
            ("Payback (years)", Opt(metrics.PaybackPeriodYears))
        });

        pdf.Section("Plant summary");
        pdf.KeyValueRows(new (string, string)[]
        {
            ("Total feed (t/day)", N((double)run.TotalFeedTpd)),
            ("Blended C:N ratio", N((double)run.BlendedCnRatio)),
            ("Raw biogas (Nm3/day)", N((double)run.RawBiogasNm3Day)),
            ("Methane (%)", N((double)run.Ch4Percent)),
            ("Net Bio-CNG (kg/day)", N((double)run.NetCbgKgDay)),
            ("Parasitic power (kW)", N((double)run.ParasiticPowerKw)),
            ("Parasitic heat (kW)", N((double)run.ParasiticHeatKw))
        });

        pdf.Section("Major equipment");
        pdf.Table(
            new[] { "Tag", "Equipment", "Qty", "Total cost (INR)" },
            new[] { 80.0, 265.0, 50.0, 120.0 },
            new[] { false, false, true, true },
            data.Equipment
                .Select(e => new[]
                {
                    e.Tag,
                    Pretty(e.Type),
                    e.Quantity.ToString(inv),
                    e.TotalCost.ToString("N2", inv)
                })
                .ToList(),
            new[] { string.Empty, "Total", string.Empty, data.EquipmentCapex.ToString("N2", inv) });

        pdf.Section("Financial summary (INR)");
        pdf.KeyValueRows(new (string, string)[]
        {
            ("Total CAPEX", N(metrics.CapexTotal)),
            ("Annual OPEX", N(metrics.OpexAnnual)),
            ("Annual revenue", N(metrics.AnnualRevenue)),
            ("NPV", N(metrics.Npv)),
            ("IRR (%)", Opt(metrics.IrrPercent)),
            ("Payback period (years)", Opt(metrics.PaybackPeriodYears)),
            ("Minimum DSCR", Opt(metrics.MinDscr))
        });

        if (data.Result.Warnings.Count > 0)
        {
            pdf.Section("Engineering warnings");
            pdf.Bullets(data.Result.Warnings);
        }

        return pdf.Build();
    }

    // ------------------------------------------------------------------ storage

    private async Task<Guid> SaveArtifactAsync(
        Guid runId,
        ArtifactType type,
        string fileName,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream(bytes);

        var checksum = await _storage.ComputeSha256Async(buffer, cancellationToken);
        buffer.Position = 0;
        var fileUrl = await _storage.SaveAsync(runId, fileName, buffer, cancellationToken);

        var artifact = new GeneratedArtifact
        {
            Id = Guid.NewGuid(),
            SimulationRunId = runId,
            ArtifactType = type,
            FileUrl = fileUrl,
            Checksum = checksum,
            CreatedAt = DateTime.UtcNow
        };

        _db.GeneratedArtifacts.Add(artifact);
        await _db.SaveChangesAsync(cancellationToken);

        return artifact.Id;
    }

    // ------------------------------------------------------------------ PDF writer

    /// <summary>
    /// Dependency-free PDF writer: A4, built-in Helvetica, simple banner, boxes, ruled tables and footer.
    /// Non-ASCII characters are replaced with '?'.
    /// </summary>
    private sealed class ProposalPdf
    {
        private const double PageW = 595;
        private const double PageH = 842;
        private const double Margin = 40;
        private const double ContentW = PageW - 2 * Margin;
        private const double BottomLimit = 42;

        private const string Teal = "0.059 0.463 0.431";
        private const string Ink = "0.059 0.090 0.165";
        private const string Muted = "0.392 0.455 0.545";
        private const string Rule = "0.886 0.910 0.941";
        private const string Soft = "0.945 0.961 0.976";
        private const string White = "1 1 1";

        // Helvetica glyph widths for ASCII 32..126 (1/1000 em).
        private static readonly int[] Widths =
        {
            278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
            556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
            278, 278, 584, 584, 584, 556, 1015,
            667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833,
            722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611,
            278, 278, 278, 469, 556, 333,
            556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833,
            556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500,
            334, 260, 334, 584
        };

        private readonly List<StringBuilder> _pages = new();
        private StringBuilder _page = null!;
        private double _y;

        public ProposalPdf()
        {
            NewPage();
        }

        // ---------------------------------------------------------- layout

        public void Banner(string title, string subtitle)
        {
            DrawRect(0, PageH - 84, PageW, 84, Teal);
            DrawText(Margin, PageH - 40, title, 20, true, White);
            DrawText(Margin, PageH - 62, subtitle, 10.5, false, White);
            _y = PageH - 84 - 28;
        }

        public void InfoRows(IEnumerable<(string Label, string Value)> rows)
        {
            foreach (var (label, value) in rows)
            {
                EnsureSpace(16);
                DrawText(Margin, _y, label, 9.5, false, Muted);
                DrawText(Margin + 120, _y, value, 9.5, true, Ink);
                _y -= 16;
            }
            _y -= 6;
        }

        public void MetricBoxes(IReadOnlyList<(string Label, string Value)> items)
        {
            const double gap = 8;
            const double boxH = 46;
            var boxW = (ContentW - gap * (items.Count - 1)) / items.Count;

            EnsureSpace(boxH + 10);
            for (var i = 0; i < items.Count; i++)
            {
                var x = Margin + i * (boxW + gap);
                DrawRect(x, _y - boxH, boxW, boxH, Soft);
                DrawRect(x, _y - boxH, 3, boxH, Teal);
                DrawText(x + 12, _y - 16, items[i].Label, 8, false, Muted);
                DrawText(x + 12, _y - 36, items[i].Value, 12.5, true, Ink);
            }
            _y -= boxH + 14;
        }

        public void Section(string title)
        {
            EnsureSpace(60);
            DrawText(Margin, _y, title.ToUpperInvariant(), 10, true, Teal);
            DrawLine(Margin, PageW - Margin, _y - 6, Teal, 0.8);
            _y -= 22;
        }

        public void KeyValueRows(IEnumerable<(string Label, string Value)> rows)
        {
            foreach (var (label, value) in rows)
            {
                EnsureSpace(18);
                DrawText(Margin + 4, _y - 2, label, 9.5, false, Ink);
                DrawTextRight(PageW - Margin - 4, _y - 2, value, 9.5, true, Ink);
                DrawLine(Margin, PageW - Margin, _y - 8, Rule, 0.5);
                _y -= 18;
            }
            _y -= 8;
        }

        public void Table(
            string[] headers,
            double[] widths,
            bool[] rightAlign,
            IReadOnlyList<string[]> rows,
            string[]? totalRow = null)
        {
            void Header()
            {
                DrawRect(Margin, _y - 14, ContentW, 20, Soft);
                var hx = Margin;
                for (var c = 0; c < headers.Length; c++)
                {
                    if (rightAlign[c])
                        DrawTextRight(hx + widths[c] - 6, _y - 7, headers[c], 8.5, true, Muted);
                    else
                        DrawText(hx + 6, _y - 7, headers[c], 8.5, true, Muted);
                    hx += widths[c];
                }
                _y -= 26;
            }

            void Row(string[] cells, bool bold)
            {
                var rx = Margin;
                for (var c = 0; c < cells.Length; c++)
                {
                    if (rightAlign[c])
                        DrawTextRight(rx + widths[c] - 6, _y - 2, cells[c], 9, bold, Ink);
                    else
                        DrawText(rx + 6, _y - 2, cells[c], 9, bold, Ink);
                    rx += widths[c];
                }
                DrawLine(Margin, PageW - Margin, _y - 8, bold ? Ink : Rule, bold ? 0.8 : 0.5);
                _y -= 18;
            }

            EnsureSpace(70);
            Header();

            foreach (var row in rows)
            {
                if (_y - 18 < BottomLimit)
                {
                    NewPage();
                    Header();
                }
                Row(row, false);
            }

            if (totalRow != null)
            {
                if (_y - 18 < BottomLimit)
                {
                    NewPage();
                    Header();
                }
                Row(totalRow, true);
            }

            _y -= 8;
        }

        public void Bullets(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                var lines = Wrap(item, 9.5, ContentW - 16);
                EnsureSpace(lines.Count * 13 + 4);
                DrawText(Margin + 4, _y - 2, "-", 9.5, true, Teal);
                foreach (var line in lines)
                {
                    DrawText(Margin + 16, _y - 2, line, 9.5, false, Ink);
                    _y -= 13;
                }
                _y -= 4;
            }
        }

        public byte[] Build()
        {
            var total = _pages.Count;
            for (var i = 0; i < total; i++)
            {
                _page = _pages[i];
                DrawLine(Margin, PageW - Margin, 38, Rule, 0.5);
                DrawText(Margin, 24, "BiogasApp - Techno-Economic Proposal", 8, false, Muted);
                DrawTextRight(PageW - Margin, 24, $"Page {i + 1} of {total}", 8, false, Muted);
            }

            // Objects: 1 catalog, 2 pages, 3 regular font, 4 bold font, then (page, content) pairs.
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
            };

            var kids = new List<string>();
            for (var i = 0; i < total; i++)
            {
                var pageId = 5 + i * 2;
                var contentId = pageId + 1;
                kids.Add($"{pageId} 0 R");

                var stream = _pages[i].ToString();
                objects.Add(
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentId} 0 R " +
                    "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> >>");
                objects.Add($"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream");
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", kids)}] /Count {total} >>";

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xrefPosition = pdf.Length;
            pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                pdf.Append($"{offset:D10} 00000 n \n");

            pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");

            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        // ---------------------------------------------------------- helpers

        private void NewPage()
        {
            _page = new StringBuilder();
            _pages.Add(_page);
            _y = PageH - Margin;
        }

        private void EnsureSpace(double needed)
        {
            if (_y - needed < BottomLimit)
                NewPage();
        }

        private void DrawText(double x, double y, string text, double size, bool bold, string color)
        {
            _page.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(F(size)).Append(" Tf ")
                .Append(color).Append(" rg ").Append(F(x)).Append(' ').Append(F(y)).Append(" Td (")
                .Append(Escape(text)).Append(") Tj ET\n");
        }

        private void DrawTextRight(double xRight, double y, string text, double size, bool bold, string color)
        {
            DrawText(xRight - Measure(text, size), y, text, size, bold, color);
        }

        private void DrawRect(double x, double y, double w, double h, string color)
        {
            _page.Append(color).Append(" rg ")
                .Append(F(x)).Append(' ').Append(F(y)).Append(' ')
                .Append(F(w)).Append(' ').Append(F(h)).Append(" re f\n");
        }

        private void DrawLine(double x1, double x2, double y, string color, double width)
        {
            _page.Append(color).Append(" RG ").Append(F(width)).Append(" w ")
                .Append(F(x1)).Append(' ').Append(F(y)).Append(" m ")
                .Append(F(x2)).Append(' ').Append(F(y)).Append(" l S\n");
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static double Measure(string text, double size)
        {
            double total = 0;
            foreach (var ch in text)
            {
                var index = ch >= ' ' && ch <= '~' ? ch - 32 : '?' - 32;
                total += Widths[index];
            }
            return total * size / 1000.0;
        }

        private static List<string> Wrap(string text, double size, double maxWidth)
        {
            var result = new List<string>();
            var current = new StringBuilder();

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current.ToString() + " " + word;
                if (current.Length > 0 && Measure(candidate, size) > maxWidth)
                {
                    result.Add(current.ToString());
                    current.Clear().Append(word);
                }
                else
                {
                    current.Clear().Append(candidate);
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());
            if (result.Count == 0)
                result.Add(string.Empty);

            return result;
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch is '\\' or '(' or ')')
                    sb.Append('\\').Append(ch);
                else if (ch < 32 || ch > 126)
                    sb.Append('?');
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}