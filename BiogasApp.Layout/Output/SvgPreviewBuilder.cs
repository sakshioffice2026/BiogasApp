using System.Globalization;
using System.Net;
using System.Text;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Models;
using BiogasApp.Layout.Placement;

namespace BiogasApp.Layout.Output;

public static class SvgPreviewBuilder
{
    private const double Margin = 40.0;

    public static string Build(LayoutInput input, LayoutResult result, IReadOnlyList<RoadSegment> roads)
    {
        var (bbMin, bbMax) = PolygonOps.BoundingBox(input.PlotCorners);
        double minX = bbMin.X - Margin, maxX = bbMax.X + Margin;
        double minY = bbMin.Y - Margin, maxY = bbMax.Y + Margin;
        double w = maxX - minX, h = maxY - minY;

        // SVG Y axis points down, so plan Y is flipped
        double Fy(double y) => minY + maxY - y;
        string Pts(IEnumerable<Point2D> pts) => string.Join(" ", pts.Select(p => $"{F(p.X)},{F(Fy(p.Y))}"));

        var warned = result.Warnings
            .Where(x => x.EquipmentId != null)
            .Select(x => (x.EquipmentId!.Value, x.InstanceNo ?? 1))
            .ToHashSet();

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"layout-svg\" ");
        sb.Append($"viewBox=\"{F(minX)} {F(minY)} {F(w)} {F(h)}\" preserveAspectRatio=\"xMidYMid meet\" ");
        sb.Append($"data-min-x=\"{F(minX)}\" data-max-y=\"{F(maxY)}\" data-min-y=\"{F(minY)}\">");

        sb.Append($"<polygon class=\"plot\" points=\"{Pts(input.PlotCorners)}\" fill=\"#fffef5\" stroke=\"#d32f2f\" stroke-width=\"0.6\"/>");

        foreach (var z in input.NoBuildZones.Where(z => z.Polygon.Count >= 3))
        {
            sb.Append($"<polygon class=\"nobuild\" points=\"{Pts(z.Polygon)}\" fill=\"#9c27b0\" fill-opacity=\"0.15\" stroke=\"#9c27b0\" stroke-dasharray=\"2 1\" stroke-width=\"0.4\"/>");
            var c = PolygonOps.Centroid(z.Polygon);
            sb.Append($"<text x=\"{F(c.X)}\" y=\"{F(Fy(c.Y))}\" font-size=\"2\" text-anchor=\"middle\" fill=\"#9c27b0\">NO-BUILD: {WebUtility.HtmlEncode(z.Reason.ToString())}</text>");
        }

        foreach (var road in roads)
        {
            sb.Append($"<polyline class=\"road\" points=\"{Pts(road.Points)}\" fill=\"none\" stroke=\"#9e9e9e\" stroke-opacity=\"0.45\" stroke-width=\"{F(road.WidthM)}\" stroke-linejoin=\"miter\" stroke-linecap=\"square\"/>");
            sb.Append($"<polyline class=\"road-cl\" points=\"{Pts(road.Points)}\" fill=\"none\" stroke=\"#f9a825\" stroke-width=\"0.2\" stroke-dasharray=\"2 1\"/>");
        }

        foreach (var item in result.Items)
        {
            string color = item.Zone switch
            {
                EquipmentZone.Dirty => "#b5651d",
                EquipmentZone.Process => "#2e8b57",
                EquipmentZone.Gas => "#00acc1",
                _ => "#3f51b5"
            };
            bool hasWarning = warned.Contains((item.EquipmentId, item.InstanceNo));
            string stroke = hasWarning ? "#d32f2f" : color;
            string strokeW = hasWarning ? "0.8" : "0.4";
            string tag = item.InstanceNo > 1 ? $"{item.Tag}-{item.InstanceNo}" : item.Tag;

            sb.Append($"<g class=\"item{(hasWarning ? " has-warning" : "")}{(item.IsLocked ? " locked" : "")}\" ");
            sb.Append($"data-eq=\"{item.EquipmentId}\" data-inst=\"{item.InstanceNo}\" ");
            sb.Append($"data-x=\"{F(item.X)}\" data-y=\"{F(item.Y)}\" data-rot=\"{F(item.RotationDeg)}\" ");
            sb.Append($"data-locked=\"{(item.IsLocked ? "1" : "0")}\">");

            if (item.Shape == EquipmentShape.Circle)
                sb.Append($"<circle cx=\"{F(item.X)}\" cy=\"{F(Fy(item.Y))}\" r=\"{F(item.WidthM / 2)}\" fill=\"{color}\" fill-opacity=\"0.25\" stroke=\"{stroke}\" stroke-width=\"{strokeW}\"/>");
            else
                sb.Append($"<polygon points=\"{Pts(item.GetFootprint())}\" fill=\"{color}\" fill-opacity=\"0.25\" stroke=\"{stroke}\" stroke-width=\"{strokeW}\"/>");

            double fs = Math.Clamp(Math.Min(item.WidthM, item.LengthM) / 4.0, 1.2, 3.5);
            sb.Append($"<text x=\"{F(item.X)}\" y=\"{F(Fy(item.Y))}\" font-size=\"{F(fs)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"#222\" pointer-events=\"none\">{WebUtility.HtmlEncode(tag)}</text>");
            sb.Append("<title>").Append(WebUtility.HtmlEncode(item.Name)).Append("</title></g>");
        }

        foreach (var r in input.Receptors.Where(r =>
                     r.Position.X >= minX && r.Position.X <= maxX && r.Position.Y >= minY && r.Position.Y <= maxY))
        {
            string label = string.IsNullOrWhiteSpace(r.Name) ? r.Type.ToString() : $"{r.Type}: {r.Name}";
            sb.Append($"<circle class=\"receptor\" cx=\"{F(r.Position.X)}\" cy=\"{F(Fy(r.Position.Y))}\" r=\"2\" fill=\"#ff7043\"/>");
            sb.Append($"<text x=\"{F(r.Position.X)}\" y=\"{F(Fy(r.Position.Y) - 3)}\" font-size=\"2\" text-anchor=\"middle\" fill=\"#bf360c\">{WebUtility.HtmlEncode(label)}</text>");
        }

        var g = result.GatePoint;
        sb.Append($"<circle class=\"gate\" cx=\"{F(g.X)}\" cy=\"{F(Fy(g.Y))}\" r=\"2\" fill=\"#fbc02d\" stroke=\"#333\" stroke-width=\"0.3\"/>");
        sb.Append($"<text x=\"{F(g.X)}\" y=\"{F(Fy(g.Y) + 5)}\" font-size=\"2.2\" text-anchor=\"middle\" fill=\"#333\">GATE</text>");

        var north = ZoneAssigner.BearingToVector(0, input.NorthAngleDeg);
        var o = new Point2D(bbMax.X + 15, bbMax.Y + 10);
        var tip = o + north * 12;
        sb.Append($"<line x1=\"{F(o.X)}\" y1=\"{F(Fy(o.Y))}\" x2=\"{F(tip.X)}\" y2=\"{F(Fy(tip.Y))}\" stroke=\"#333\" stroke-width=\"0.5\"/>");
        sb.Append($"<text x=\"{F(tip.X + north.X * 3)}\" y=\"{F(Fy(tip.Y + north.Y * 3))}\" font-size=\"3\" text-anchor=\"middle\" dominant-baseline=\"middle\">N</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}