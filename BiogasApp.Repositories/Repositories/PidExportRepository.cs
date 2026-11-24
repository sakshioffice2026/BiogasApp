using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using ClosedXML.Excel;

namespace BiogasApp.Repositories.Repositories;

public class PidExportRepository : IPidExport
{
    private const int HeaderRow = 6;

    private readonly IPidRepository _pid;

    public PidExportRepository(IPidRepository pid)
    {
        _pid = pid;
    }

    public async Task<byte[]> BuildListsExcelAsync(Guid pidDiagramId, CancellationToken cancellationToken = default)
    {
        var d = await _pid.GetByIdAsync(pidDiagramId, cancellationToken)
            ?? throw new InvalidOperationException("P&ID not found.");

        var projectName = await _pid.GetProjectNameAsync(d.ProjectId, cancellationToken) ?? string.Empty;

        var nodeTag = d.Nodes.ToDictionary(n => n.Id, n => n.Tag);
        var lineNo = d.Lines.ToDictionary(l => l.Id, l => l.LineNo);

        using var wb = new XLWorkbook();

        BuildLineList(wb.AddWorksheet("Line List"), d, projectName, nodeTag);
        BuildValveList(wb.AddWorksheet("Valve List"), d, projectName, lineNo);
        BuildInstrumentList(wb.AddWorksheet("Instrument List"), d, projectName, lineNo, nodeTag);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildLineList(IXLWorksheet ws, PidDiagramDto d, string projectName, Dictionary<Guid, string> nodeTag)
    {
        WriteHeaderBlock(ws, d, projectName, "LINE LIST");

        var headers = new[] { "Line No", "From", "To", "Medium", "Size DN (mm)", "Material", "Insulated", "Notes" };
        WriteTableHeader(ws, headers);

        var r = HeaderRow + 1;
        foreach (var l in d.Lines.OrderBy(x => x.LineNo, StringComparer.OrdinalIgnoreCase))
        {
            SetText(ws, r, 1, l.LineNo);
            SetText(ws, r, 2, nodeTag.GetValueOrDefault(l.FromNodeId));
            SetText(ws, r, 3, nodeTag.GetValueOrDefault(l.ToNodeId));
            SetText(ws, r, 4, l.Medium.ToString());
            SetNumber(ws, r, 5, l.NominalSizeMm);
            SetText(ws, r, 6, l.Material);
            SetText(ws, r, 7, l.IsInsulated ? "Yes" : "No");
            SetText(ws, r, 8, l.Notes);
            r++;
        }

        Finish(ws, headers.Length, r - 1);
    }

    private static void BuildValveList(IXLWorksheet ws, PidDiagramDto d, string projectName, Dictionary<Guid, string> lineNo)
    {
        WriteHeaderBlock(ws, d, projectName, "VALVE LIST");

        var headers = new[] { "Valve Tag", "Type", "Line No", "Size DN (mm)", "Normal Position", "Notes" };
        WriteTableHeader(ws, headers);

        var r = HeaderRow + 1;
        foreach (var v in d.Valves.OrderBy(x => x.Tag, StringComparer.OrdinalIgnoreCase))
        {
            SetText(ws, r, 1, v.Tag);
            SetText(ws, r, 2, v.ValveType.ToString());
            SetText(ws, r, 3, lineNo.GetValueOrDefault(v.PidLineId));
            SetNumber(ws, r, 4, v.NominalSizeMm);
            SetText(ws, r, 5, v.IsNormallyOpen ? "Open" : "Closed");
            SetText(ws, r, 6, v.Notes);
            r++;
        }

        Finish(ws, headers.Length, r - 1);
    }

    private static void BuildInstrumentList(
        IXLWorksheet ws, PidDiagramDto d, string projectName,
        Dictionary<Guid, string> lineNo, Dictionary<Guid, string> nodeTag)
    {
        WriteHeaderBlock(ws, d, projectName, "INSTRUMENT LIST");

        var headers = new[] { "Instrument Tag", "Type", "Service", "Location", "Range Min", "Range Max", "Unit", "Notes" };
        WriteTableHeader(ws, headers);

        var r = HeaderRow + 1;
        foreach (var i in d.Instruments.OrderBy(x => x.Tag, StringComparer.OrdinalIgnoreCase))
        {
            string? location = null;
            if (i.PidEquipmentNodeId.HasValue) location = nodeTag.GetValueOrDefault(i.PidEquipmentNodeId.Value);
            else if (i.PidLineId.HasValue) location = lineNo.GetValueOrDefault(i.PidLineId.Value);

            SetText(ws, r, 1, i.Tag);
            SetText(ws, r, 2, i.InstrumentType.ToString());
            SetText(ws, r, 3, i.Service);
            SetText(ws, r, 4, location);
            SetNumber(ws, r, 5, i.RangeMin);
            SetNumber(ws, r, 6, i.RangeMax);
            SetText(ws, r, 7, i.Unit);
            SetText(ws, r, 8, i.Notes);
            r++;
        }

        Finish(ws, headers.Length, r - 1);
    }

    private static void WriteHeaderBlock(IXLWorksheet ws, PidDiagramDto d, string projectName, string title)
    {
        SetText(ws, 1, 1, title);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        SetText(ws, 2, 1, "Project");
        SetText(ws, 2, 2, projectName);
        SetText(ws, 3, 1, "Drawing No");
        SetText(ws, 3, 2, d.DrawingNo);
        SetText(ws, 4, 1, "Revision / Status");
        SetText(ws, 4, 2, $"Rev {d.RevisionNo} / {d.Status}");

        ws.Range(2, 1, 4, 1).Style.Font.Bold = true;
    }

    private static void WriteTableHeader(IXLWorksheet ws, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
            SetText(ws, HeaderRow, c + 1, headers[c]);

        var range = ws.Range(HeaderRow, 1, HeaderRow, headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
    }

    private static void Finish(IXLWorksheet ws, int columnCount, int lastRow)
    {
        if (lastRow >= HeaderRow)
        {
            var table = ws.Range(HeaderRow, 1, lastRow, columnCount);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.SheetView.FreezeRows(HeaderRow);
        ws.Columns().AdjustToContents();
    }

    private static void SetText(IXLWorksheet ws, int row, int col, string? value)
    {
        ws.Cell(row, col).Value = value ?? string.Empty;
    }

    private static void SetNumber(IXLWorksheet ws, int row, int col, decimal? value)
    {
        if (value.HasValue) ws.Cell(row, col).Value = value.Value;
    }
}
