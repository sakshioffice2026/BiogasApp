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

        var run = data.Run;

        var lines = new List<string>
        {
            "BIOGAS / BIO-CNG PLANT - TECHNO-ECONOMIC PROPOSAL",
            new string('=', 60),
            $"Project            : {data.Project.Name}",
            $"Location           : {data.Project.LocationCountry}",
            $"Simulation version : {run.RunVersion}",
            $"Generated (UTC)    : {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            "",
            "PLANT SUMMARY",
            new string('-', 60),
            $"Total feed (t/day)        : {N((double)run.TotalFeedTpd)}",
            $"Blended C:N ratio         : {N((double)run.BlendedCnRatio)}",
            $"Raw biogas (Nm3/day)      : {N((double)run.RawBiogasNm3Day)}",
            $"Methane (%)               : {N((double)run.Ch4Percent)}",
            $"Net Bio-CNG (kg/day)      : {N((double)run.NetCbgKgDay)}",
            $"Parasitic power (kW)      : {N((double)run.ParasiticPowerKw)}",
            $"Parasitic heat (kW)       : {N((double)run.ParasiticHeatKw)}",
            "",
            "MAJOR EQUIPMENT",
            new string('-', 60),
            $"{"Tag",-10} {"Type",-28} {"Qty",3} {"Total cost",16}"
        };

        foreach (var e in data.Equipment)
            lines.Add($"{e.Tag,-10} {e.Type,-28} {e.Quantity,3} {e.TotalCost.ToString("N2", inv),16}");

        lines.AddRange(new[]
        {
            "",
            "FINANCIAL SUMMARY (INR)",
            new string('-', 60),
            $"Total CAPEX               : {N(metrics.CapexTotal)}",
            $"Annual OPEX               : {N(metrics.OpexAnnual)}",
            $"Annual revenue            : {N(metrics.AnnualRevenue)}",
            $"NPV                       : {N(metrics.Npv)}",
            $"IRR (%)                   : {Opt(metrics.IrrPercent)}",
            $"Payback period (years)    : {Opt(metrics.PaybackPeriodYears)}",
            $"Minimum DSCR              : {Opt(metrics.MinDscr)}"
        });

        if (data.Result.Warnings.Count > 0)
        {
            lines.Add("");
            lines.Add("ENGINEERING WARNINGS");
            lines.Add(new string('-', 60));
            lines.AddRange(data.Result.Warnings.Select(w => "* " + w));
        }

        return SimplePdf.Build(lines);
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

    // ------------------------------------------------------------------ minimal PDF writer

    /// <summary>
    /// Dependency-free PDF writer: monospaced text (built-in Courier), A4, paginated.
    /// Non-ASCII characters are replaced with '?'. Swap for a PDF library if you need rich layout.
    /// </summary>
    private static class SimplePdf
    {
        private const int LinesPerPage = 62;
        private const int MaxLineLength = 90;

        public static byte[] Build(IReadOnlyList<string> lines)
        {
            var pages = lines.Chunk(LinesPerPage).ToList();
            if (pages.Count == 0) pages.Add(Array.Empty<string>());

            // Objects: 1 catalog, 2 pages, 3 font, then (page, content) pairs.
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"
            };

            var kids = new List<string>();

            for (var i = 0; i < pages.Count; i++)
            {
                var pageId = 4 + i * 2;
                var contentId = pageId + 1;
                kids.Add($"{pageId} 0 R");

                var stream = new StringBuilder();
                stream.Append("BT /F1 9 Tf 50 800 Td 12 TL\n");
                foreach (var line in pages[i])
                    stream.Append('(').Append(Escape(line)).Append(") Tj T*\n");
                stream.Append("ET");

                objects.Add(
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentId} 0 R " +
                    "/Resources << /Font << /F1 3 0 R >> >> >>");
                objects.Add($"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream");
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", kids)}] /Count {pages.Count} >>";

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

        private static string Escape(string text)
        {
            if (text.Length > MaxLineLength)
                text = text[..MaxLineLength];

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