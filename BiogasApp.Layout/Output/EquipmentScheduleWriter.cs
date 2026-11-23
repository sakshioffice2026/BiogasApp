using BiogasApp.Database.Enums;
using BiogasApp.Layout.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BiogasApp.Layout.Output;

public static class EquipmentScheduleWriter
{
    private const double RowHeight = 6.0;
    private const double TextHeight = 2.0;

    private static readonly double[] ColWidths = { 12, 22, 70, 22, 34, 14 };
    private static readonly string[] Headers = { "S.NO", "TAG", "DESCRIPTION", "ZONE", "SIZE (m)", "QTY" };

    public static void Write(DxfDocument doc, IReadOnlyList<PlacedItem> items, double x, double y)
    {
        var layer = doc.Layers["TITLE"];
        double width = ColWidths.Sum();

        var groups = items
            .GroupBy(i => i.EquipmentId)
            .Select(g => (Item: g.First(), Qty: g.Count()))
            .ToList();

        double titleH = RowHeight;
        double total = titleH + RowHeight * (groups.Count + 1);
        double bottom = y - total;

        Add(doc, layer, new Polyline2D(new List<Vector2>
        {
            new(x, bottom), new(x + width, bottom), new(x + width, y), new(x, y)
        }, true));

        Text(doc, layer, "EQUIPMENT SCHEDULE", x + width / 2, y - titleH / 2, 3.0, TextAlignment.MiddleCenter);

        double cursor = y - titleH;
        Add(doc, layer, new Line(new Vector2(x, cursor), new Vector2(x + width, cursor)));

        DrawRow(doc, layer, x, cursor, Headers);
        cursor -= RowHeight;
        Add(doc, layer, new Line(new Vector2(x, cursor), new Vector2(x + width, cursor)));

        int no = 1;
        foreach (var (item, qty) in groups)
        {
            string tag = item.Tag;
            string[] cells =
            {
                no.ToString(),
                tag,
                item.Name.ToUpperInvariant(),
                item.Zone.ToString().ToUpperInvariant(),
                SizeText(item),
                qty.ToString()
            };
            DrawRow(doc, layer, x, cursor, cells);
            cursor -= RowHeight;
            Add(doc, layer, new Line(new Vector2(x, cursor), new Vector2(x + width, cursor)));
            no++;
        }

        double colX = x;
        for (int c = 0; c < ColWidths.Length - 1; c++)
        {
            colX += ColWidths[c];
            Add(doc, layer, new Line(new Vector2(colX, y - titleH), new Vector2(colX, bottom)));
        }
    }

    private static string SizeText(PlacedItem item)
    {
        if (item.Shape == EquipmentShape.Circle)
            return $"%%c{item.WidthM:0.##}M";
        return $"{item.WidthM:0.##}M X {item.LengthM:0.##}M";
    }

    private static void DrawRow(DxfDocument doc, Layer layer, double x, double rowTop, string[] cells)
    {
        double colX = x;
        double mid = rowTop - RowHeight / 2;
        for (int c = 0; c < cells.Length; c++)
        {
            Text(doc, layer, cells[c], colX + 1.5, mid, TextHeight, TextAlignment.MiddleLeft);
            colX += ColWidths[c];
        }
    }

    private static void Add(DxfDocument doc, Layer layer, EntityObject entity)
    {
        entity.Layer = layer;
        doc.Entities.Add(entity);
    }

    private static void Text(DxfDocument doc, Layer layer, string value, double x, double y,
        double height, TextAlignment alignment)
    {
        var t = new netDxf.Entities.Text(value, new Vector2(x, y), height) { Alignment = alignment };
        Add(doc, layer, t);
    }
}