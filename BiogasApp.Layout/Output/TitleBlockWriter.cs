using netDxf;
using netDxf.Entities;

namespace BiogasApp.Layout.Output;

public sealed record TitleBlockInfo(
    string ProjectName,
    string Location,
    string CapacityText,
    string DrawingNo,
    string Revision,
    DateTime Date,
    string RuleSetName,
    int RuleSetVersion,
    string LayoutOriginText,
    string WarningSummary);

public static class TitleBlockWriter
{
    public const string Disclaimer =
        "DRAFT LAYOUT - NOT A LEGAL OR PESO COMPLIANCE APPROVAL. VERIFY WITH A QUALIFIED ENGINEER.";

    private const double Width = 170.0;
    private const double LabelWidth = 40.0;
    private const double RowHeight = 7.0;
    private const double TextHeight = 2.2;

    public static void Write(DxfDocument doc, TitleBlockInfo info, double x, double y)
    {
        var layer = doc.Layers["TITLE"];

        var rows = new List<(string Label, string Value)>
        {
            ("PROJECT", info.ProjectName),
            ("LOCATION", info.Location),
            ("CAPACITY", info.CapacityText),
            ("DRAWING NO / REV", $"{info.DrawingNo} / {info.Revision}"),
            ("DATE", info.Date.ToString("dd-MMM-yyyy").ToUpperInvariant()),
            ("RULE SET", $"{info.RuleSetName} V{info.RuleSetVersion}"),
            ("LAYOUT TYPE", info.LayoutOriginText),
            ("WARNINGS", info.WarningSummary)
        };

        double titleH = RowHeight + 2;
        double disclaimerH = RowHeight;
        double total = titleH + rows.Count * RowHeight + disclaimerH;
        double top = y;
        double bottom = y - total;

        AddEntity(doc, layer, new Polyline2D(new List<Vector2>
        {
            new(x, bottom), new(x + Width, bottom), new(x + Width, top), new(x, top)
        }, true));

        double cursor = top - titleH;
        AddText(doc, layer, "GENERAL ARRANGEMENT - SITE PLAN", x + Width / 2, top - titleH / 2, 3.5, TextAlignment.MiddleCenter);
        AddEntity(doc, layer, new Line(new Vector2(x, cursor), new Vector2(x + Width, cursor)));

        foreach (var (label, value) in rows)
        {
            double mid = cursor - RowHeight / 2;
            AddText(doc, layer, label, x + 2, mid, TextHeight, TextAlignment.MiddleLeft);
            AddText(doc, layer, value, x + LabelWidth + 2, mid, TextHeight, TextAlignment.MiddleLeft);
            cursor -= RowHeight;
            AddEntity(doc, layer, new Line(new Vector2(x, cursor), new Vector2(x + Width, cursor)));
        }

        AddEntity(doc, layer, new Line(
            new Vector2(x + LabelWidth, top - titleH), new Vector2(x + LabelWidth, cursor)));

        AddText(doc, layer, Disclaimer, x + 2, cursor - disclaimerH / 2, 1.8, TextAlignment.MiddleLeft);
    }

    private static void AddEntity(DxfDocument doc, netDxf.Tables.Layer layer, EntityObject entity)
    {
        entity.Layer = layer;
        doc.Entities.Add(entity);
    }

    private static void AddText(DxfDocument doc, netDxf.Tables.Layer layer, string value,
        double x, double y, double height, TextAlignment alignment)
    {
        var t = new Text(value, new Vector2(x, y), height) { Alignment = alignment };
        AddEntity(doc, layer, t);
    }
}