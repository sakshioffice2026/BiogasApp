using System.Text.Json;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Business.Pid;

public class PidAutoLayoutService
{
    public const decimal OriginX = 150m;
    public const decimal OriginY = 150m;
    public const decimal ColumnGap = 320m;
    public const decimal RowGap = 190m;
    private const decimal Tol = 1m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static (decimal W, decimal H) SymbolSize(PidSymbolType type) => type switch
    {
        PidSymbolType.Tank => (80m, 100m),
        PidSymbolType.Digester => (120m, 140m),
        PidSymbolType.Pump => (40m, 40m),
        PidSymbolType.Valve => (20m, 20m),
        PidSymbolType.Blower => (50m, 50m),
        PidSymbolType.Flare => (40m, 80m),
        PidSymbolType.GasHolder => (110m, 110m),
        PidSymbolType.Vessel => (100m, 70m),
        PidSymbolType.Instrument => (30m, 30m),
        _ => (80m, 80m)
    };

    public void Apply(PidDiagram diagram)
    {
        PlaceNodes(diagram);
        var routes = RouteLines(diagram);
        PlaceValves(diagram, routes);
        PlaceInstruments(diagram, routes);
    }

    private static void PlaceNodes(PidDiagram diagram)
    {
        foreach (var stage in diagram.Nodes.GroupBy(x => x.StageNo).OrderBy(g => g.Key))
        {
            var row = 0;
            foreach (var node in stage)
            {
                if (!node.IsLocked)
                {
                    node.X = OriginX + (stage.Key - 1) * ColumnGap;
                    node.Y = OriginY + row * RowGap;
                }
                row++;
            }
        }
    }

    private static Dictionary<Guid, List<(decimal X, decimal Y)>> RouteLines(PidDiagram diagram)
    {
        var nodes = diagram.Nodes.ToDictionary(x => x.Id);
        var routes = new Dictionary<Guid, List<(decimal X, decimal Y)>>();
        var gapCount = new Dictionary<int, int>();

        foreach (var line in diagram.Lines)
        {
            if (!nodes.TryGetValue(line.FromNodeId, out var a)) continue;
            if (!nodes.TryGetValue(line.ToNodeId, out var b)) continue;

            var (wa, ha) = SymbolSize(a.SymbolType);
            var (wb, hb) = SymbolSize(b.SymbolType);
            var pts = new List<(decimal X, decimal Y)>();
            var dx = b.X - a.X;

            if (dx > Tol)
            {
                var sx = a.X + wa / 2;
                var ex = b.X - wb / 2;

                if (Math.Abs(a.Y - b.Y) <= Tol)
                {
                    pts.Add((sx, a.Y));
                    pts.Add((ex, a.Y));
                }
                else
                {
                    gapCount.TryGetValue(a.StageNo, out var k);
                    gapCount[a.StageNo] = k + 1;
                    var mid = sx + (ex - sx) / 2 + (k % 5 - 2) * 12;
                    if (mid <= sx + 5 || mid >= ex - 5) mid = sx + (ex - sx) / 2;
                    pts.Add((sx, a.Y));
                    pts.Add((mid, a.Y));
                    pts.Add((mid, b.Y));
                    pts.Add((ex, b.Y));
                }
            }
            else if (dx >= -Tol)
            {
                var down = b.Y >= a.Y;
                var sy = down ? a.Y + ha / 2 : a.Y - ha / 2;
                var ey = down ? b.Y - hb / 2 : b.Y + hb / 2;
                pts.Add((a.X, sy));
                pts.Add((a.X, ey));
            }
            else
            {
                var sx = a.X + wa / 2;
                var ex = b.X - wb / 2;
                var yMid = Math.Max(a.Y + ha / 2, b.Y + hb / 2) + 60m;
                pts.Add((sx, a.Y));
                pts.Add((sx + 25m, a.Y));
                pts.Add((sx + 25m, yMid));
                pts.Add((ex - 25m, yMid));
                pts.Add((ex - 25m, b.Y));
                pts.Add((ex, b.Y));
            }

            Clean(pts);
            routes[line.Id] = pts;
            line.PointsJson = JsonSerializer.Serialize(
                pts.Select(p => new PidPointDto { X = p.X, Y = p.Y }).ToList(), JsonOptions);
        }

        return routes;
    }

    private static void PlaceValves(PidDiagram diagram, Dictionary<Guid, List<(decimal X, decimal Y)>> routes)
    {
        foreach (var valve in diagram.Valves)
        {
            if (!routes.TryGetValue(valve.PidLineId, out var pts)) continue;
            var p = PointAt(pts, valve.PositionOnLine);
            valve.X = p.X;
            valve.Y = p.Y;
            valve.RotationDeg = p.Horizontal ? 0m : 90m;
        }
    }

    private static void PlaceInstruments(PidDiagram diagram, Dictionary<Guid, List<(decimal X, decimal Y)>> routes)
    {
        var nodes = diagram.Nodes.ToDictionary(x => x.Id);
        var perNode = new Dictionary<Guid, int>();

        foreach (var ins in diagram.Instruments)
        {
            if (ins.PidEquipmentNodeId.HasValue && nodes.TryGetValue(ins.PidEquipmentNodeId.Value, out var node))
            {
                var (_, h) = SymbolSize(node.SymbolType);
                perNode.TryGetValue(node.Id, out var k);
                perNode[node.Id] = k + 1;
                ins.X = node.X + k * 40m - 20m;
                ins.Y = node.Y - h / 2 - 45m;
            }
            else if (ins.PidLineId.HasValue && routes.TryGetValue(ins.PidLineId.Value, out var pts))
            {
                var p = PointAt(pts, 0.5m);
                ins.X = p.Horizontal ? p.X : p.X + 35m;
                ins.Y = p.Horizontal ? p.Y - 35m : p.Y;
            }
        }
    }

    private static void Clean(List<(decimal X, decimal Y)> pts)
    {
        for (var i = pts.Count - 1; i > 0; i--)
        {
            if (pts[i].X == pts[i - 1].X && pts[i].Y == pts[i - 1].Y)
                pts.RemoveAt(i);
        }

        for (var i = pts.Count - 2; i > 0; i--)
        {
            var p0 = pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            if ((p0.X == p1.X && p1.X == p2.X) || (p0.Y == p1.Y && p1.Y == p2.Y))
                pts.RemoveAt(i);
        }
    }

    private static (decimal X, decimal Y, bool Horizontal) PointAt(List<(decimal X, decimal Y)> pts, decimal fraction)
    {
        if (pts.Count == 0) return (0m, 0m, true);
        if (pts.Count == 1) return (pts[0].X, pts[0].Y, true);

        var total = 0m;
        for (var i = 1; i < pts.Count; i++)
            total += Math.Abs(pts[i].X - pts[i - 1].X) + Math.Abs(pts[i].Y - pts[i - 1].Y);

        var target = total * Math.Clamp(fraction, 0m, 1m);
        var run = 0m;

        for (var i = 1; i < pts.Count; i++)
        {
            var seg = Math.Abs(pts[i].X - pts[i - 1].X) + Math.Abs(pts[i].Y - pts[i - 1].Y);
            var horizontal = pts[i].Y == pts[i - 1].Y;
            if (seg > 0 && run + seg >= target)
            {
                var t = (target - run) / seg;
                return (
                    pts[i - 1].X + (pts[i].X - pts[i - 1].X) * t,
                    pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * t,
                    horizontal);
            }
            run += seg;
        }

        var last = pts[^1];
        return (last.X, last.Y, true);
    }
}
