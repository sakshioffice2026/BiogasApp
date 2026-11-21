using System.Globalization;
using System.Text.Json;
using BiogasApp.Layout.Geometry;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;

namespace BiogasApp.Repositories.Repositories;

public class OsmReceptorProvider : IReceptorProvider
{
    private const string Endpoint = "https://overpass-api.de/api/interpreter";

    private static readonly (string Type, string Filter, int Max)[] Queries =
    {
        ("School",    "[amenity~\"^(school|college|kindergarten)$\"]", 5),
        ("Hospital",  "[amenity~\"^(hospital|clinic)$\"]", 5),
        ("House",     "[building~\"^(house|residential|apartments|detached)$\"]", 10),
        ("Road",      "[highway~\"^(motorway|trunk|primary|secondary|tertiary|unclassified|residential)$\"]", 5),
        ("Canal",     "[waterway=canal]", 5),
        ("PowerLine", "[power=line]", 5)
    };

    private readonly HttpClient _http;

    public OsmReceptorProvider(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("BiogasApp/1.0");
    }

    public async Task<List<ReceptorDto>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusM,
        CancellationToken cancellationToken = default)
    {
        var inv = CultureInfo.InvariantCulture;
        string around = string.Format(inv, "(around:{0:0},{1:0.######},{2:0.######})", radiusM, latitude, longitude);

        var parts = Queries.Select(q => $"nw{around}{q.Filter};");
        string ql = $"[out:json][timeout:25];({string.Join("", parts)});out geom;";

        string body;
        try
        {
            using var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", ql) });
            using var response = await _http.PostAsync(Endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new List<ReceptorDto>();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new List<ReceptorDto>();
        }

        var geo = new GeoProjection(latitude, longitude);
        var found = new List<ReceptorDto>();

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("elements", out var elements)) return found;

        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var tags)) continue;

            var type = ClassifyType(tags);
            if (type == null) continue;

            var pts = ReadGeometry(el, geo);
            if (pts.Count == 0) continue;

            var (dist, nearest) = Nearest(pts);
            if (dist > radiusM) continue;

            var ll = geo.ToLatLon(nearest);
            string? name = tags.TryGetProperty("name", out var n) ? n.GetString() : null;

            found.Add(new ReceptorDto
            {
                Type = type,
                Name = string.IsNullOrWhiteSpace(name) ? type : name,
                Latitude = ll.Lat,
                Longitude = ll.Lon,
                DistanceFromPlotM = Math.Round(dist, 1),
                Source = "MapData",
                IsConfirmed = false
            });
        }

        var result = new List<ReceptorDto>();
        foreach (var q in Queries)
        {
            result.AddRange(found.Where(f => f.Type == q.Type)
                .OrderBy(f => f.DistanceFromPlotM)
                .Take(q.Max));
        }

        return result;
    }

    private static string? ClassifyType(JsonElement tags)
    {
        string? Get(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;

        var amenity = Get("amenity");
        if (amenity is "school" or "college" or "kindergarten") return "School";
        if (amenity is "hospital" or "clinic") return "Hospital";
        if (Get("building") != null) return "House";
        if (Get("highway") != null) return "Road";
        if (Get("waterway") == "canal") return "Canal";
        if (Get("power") == "line") return "PowerLine";
        return null;
    }

    private static List<Point2D> ReadGeometry(JsonElement el, GeoProjection geo)
    {
        var pts = new List<Point2D>();

        if (el.TryGetProperty("geometry", out var g))
        {
            foreach (var p in g.EnumerateArray())
                pts.Add(geo.ToMetres(p.GetProperty("lat").GetDouble(), p.GetProperty("lon").GetDouble()));
        }
        else if (el.TryGetProperty("lat", out var lat) && el.TryGetProperty("lon", out var lon))
        {
            pts.Add(geo.ToMetres(lat.GetDouble(), lon.GetDouble()));
        }

        return pts;
    }

    // Distance from the plot origin (0,0) to the nearest point of the geometry
    private static (double Dist, Point2D Point) Nearest(List<Point2D> pts)
    {
        var origin = new Point2D(0, 0);

        if (pts.Count == 1) return (pts[0].Length, pts[0]);

        if (pts.Count >= 4 && pts[0].DistanceTo(pts[^1]) < 0.01 && PolygonOps.Contains(pts, origin))
            return (0, origin);

        double best = double.MaxValue;
        Point2D bestPt = pts[0];

        for (int i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var ab = b - a;
            double len2 = ab.Dot(ab);
            double t = len2 < 1e-9 ? 0 : Math.Clamp((origin - a).Dot(ab) / len2, 0, 1);
            var c = a + ab * t;
            double d = c.Length;
            if (d < best)
            {
                best = d;
                bestPt = c;
            }
        }

        return (best, bestPt);
    }
}