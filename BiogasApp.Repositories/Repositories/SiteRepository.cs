using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Sizing;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using BiogasApp.Layout.Sizing;
namespace BiogasApp.Repositories.Repositories;

public class SiteRepository : ISiteRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _db;

    public SiteRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SiteDto?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var site = await _db.ProjectSites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);
        if (site == null) return null;

        var corners = ReadPoints(site.PlotCornersJson);
        double lat = (double)site.Latitude;
        double lon = (double)site.Longitude;
        var geo = new GeoProjection(lat, lon);

        var dto = new SiteDto
        {
            ProjectId = projectId,
            Latitude = lat,
            Longitude = lon,
            InputMode = "Typed",
            PlotCorners = corners,
            PlotCornersLatLon = corners.Select(c =>
            {
                var ll = geo.ToLatLon(new Point2D(c.X, c.Y));
                return new LatLonDto { Lat = ll.Lat, Lon = ll.Lon };
            }).ToList(),
            PlotAreaM2 = (double)site.PlotAreaM2,
            GateEdgeIndex = site.GateEdgeIndex,
            GateOffsetM = (double)site.GateOffsetM,
            NorthAngleDeg = (double)site.NorthAngleDeg,
            WindFromDeg = ToDouble(site.WindFromDeg),
            SlopeDirectionDeg = ToDouble(site.SlopeDirectionDeg),
            PowerSideDeg = ToDouble(site.PowerSideDeg),
            GasOfftakeSideDeg = ToDouble(site.GasOfftakeSideDeg),
            Notes = site.Notes
        };

        var receptors = await _db.SiteReceptors.AsNoTracking()
            .Where(r => r.ProjectId == projectId).ToListAsync(cancellationToken);
        dto.Receptors = receptors.Select(r => new ReceptorDto
        {
            Id = r.Id,
            Type = r.Type.ToString(),
            Name = r.Name,
            Latitude = (double)r.Latitude,
            Longitude = (double)r.Longitude,
            DistanceFromPlotM = (double)r.DistanceFromPlotM,
            Source = r.Source.ToString(),
            IsConfirmed = r.IsConfirmed
        }).ToList();

        var zones = await _db.NoBuildZones.AsNoTracking()
            .Where(z => z.ProjectId == projectId).ToListAsync(cancellationToken);
        dto.NoBuildZones = zones.Select(z => new NoBuildZoneDto
        {
            Id = z.Id,
            Reason = z.Reason.ToString(),
            Polygon = ReadPoints(z.PolygonJson),
            Notes = z.Notes
        }).ToList();

        return dto;
    }

    public async Task<SiteDto> SaveAsync(SiteDto dto, CancellationToken cancellationToken = default)
    {
        var geo = new GeoProjection(dto.Latitude, dto.Longitude);
        List<PointDto> corners = dto.InputMode switch
        {
            "Map" => dto.PlotCornersLatLon
                .Select(c => geo.ToMetres(c.Lat, c.Lon))
                .Select(p => new PointDto { X = p.X, Y = p.Y }).ToList(),
            "Rectangle" when dto.RectLengthM > 0 && dto.RectWidthM > 0 => new List<PointDto>
            {
                new() { X = 0, Y = 0 },
                new() { X = dto.RectLengthM!.Value, Y = 0 },
                new() { X = dto.RectLengthM.Value, Y = dto.RectWidthM!.Value },
                new() { X = 0, Y = dto.RectWidthM.Value }
            },
            _ => dto.PlotCorners
        };

        var poly = corners.Select(c => new Point2D(c.X, c.Y)).ToList();
        double area = poly.Count >= 3 ? PolygonOps.Area(poly) : 0;

        var site = await _db.ProjectSites.FirstOrDefaultAsync(s => s.ProjectId == dto.ProjectId, cancellationToken);
        if (site == null)
        {
            site = new ProjectSite { Id = Guid.NewGuid(), ProjectId = dto.ProjectId };
            _db.ProjectSites.Add(site);
        }

        site.Latitude = (decimal)dto.Latitude;
        site.Longitude = (decimal)dto.Longitude;
        site.PlotCornersJson = JsonSerializer.Serialize(corners.Select(c => new[] { c.X, c.Y }));
        site.PlotAreaM2 = (decimal)area;
        site.GateEdgeIndex = dto.GateEdgeIndex;
        site.GateOffsetM = (decimal)dto.GateOffsetM;
        site.NorthAngleDeg = (decimal)dto.NorthAngleDeg;
        site.WindFromDeg = ToDecimal(dto.WindFromDeg);
        site.SlopeDirectionDeg = ToDecimal(dto.SlopeDirectionDeg);
        site.PowerSideDeg = ToDecimal(dto.PowerSideDeg);
        site.GasOfftakeSideDeg = ToDecimal(dto.GasOfftakeSideDeg);
        site.Notes = dto.Notes;
        site.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await SaveReceptorsAsync(dto.ProjectId, dto.Receptors, cancellationToken);
        await SaveNoBuildZonesAsync(dto.ProjectId, dto.NoBuildZones, cancellationToken);

        return (await GetByProjectAsync(dto.ProjectId, cancellationToken))!;
    }

    public async Task SaveReceptorsAsync(
        Guid projectId,
        IEnumerable<ReceptorDto> receptors,
        CancellationToken cancellationToken = default)
    {
        var site = await _db.ProjectSites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);

        var old = await _db.SiteReceptors.Where(r => r.ProjectId == projectId).ToListAsync(cancellationToken);
        _db.SiteReceptors.RemoveRange(old);

        foreach (var r in receptors)
        {
            double dist = r.DistanceFromPlotM;
            if (dist <= 0 && site != null)
                dist = GeoProjection.HaversineMetres((double)site.Latitude, (double)site.Longitude, r.Latitude, r.Longitude);

            _db.SiteReceptors.Add(new SiteReceptor
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Type = ParseEnum(r.Type, ReceptorType.Other),
                Name = r.Name,
                Latitude = (decimal)r.Latitude,
                Longitude = (decimal)r.Longitude,
                DistanceFromPlotM = (decimal)dist,
                Source = ParseEnum(r.Source, ReceptorSource.UserAdded),
                IsConfirmed = r.IsConfirmed
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveNoBuildZonesAsync(
        Guid projectId,
        IEnumerable<NoBuildZoneDto> zones,
        CancellationToken cancellationToken = default)
    {
        var old = await _db.NoBuildZones.Where(z => z.ProjectId == projectId).ToListAsync(cancellationToken);
        _db.NoBuildZones.RemoveRange(old);

        foreach (var z in zones.Where(z => z.Polygon.Count >= 3))
        {
            _db.NoBuildZones.Add(new NoBuildZone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Reason = ParseEnum(z.Reason, NoBuildReason.Other),
                PolygonJson = JsonSerializer.Serialize(z.Polygon.Select(p => new[] { p.X, p.Y })),
                Notes = z.Notes
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EquipmentItemDto>> GetEquipmentAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var items = await _db.ProjectEquipmentItems.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Tag)
            .ToListAsync(cancellationToken);

        return items.Select(e => new EquipmentItemDto
        {
            Id = e.Id,
            ProjectId = e.ProjectId,
            Tag = e.Tag,
            Name = e.Name,
            HazardClass = e.HazardClass.ToString(),
            Zone = e.Zone.ToString(),
            Shape = e.Shape.ToString(),
            WidthM = ToDouble(e.WidthM),
            LengthM = ToDouble(e.LengthM),
            DiameterM = ToDouble(e.DiameterM),
            HeightM = ToDouble(e.HeightM),
            SizeSource = e.SizeSource.ToString(),
            Quantity = e.Quantity,
            IsEnabled = e.IsEnabled,
            IsCustom = e.IsCustom,
            AllowRotation = e.AllowRotation,
            NeedsRoadAccess = e.NeedsRoadAccess
        }).ToList();
    }

    public async Task<EquipmentItemDto> SaveEquipmentItemAsync(
        EquipmentItemDto dto,
        CancellationToken cancellationToken = default)
    {
        ProjectEquipmentItem? e = null;
        if (dto.Id.HasValue)
            e = await _db.ProjectEquipmentItems.FirstOrDefaultAsync(x => x.Id == dto.Id.Value, cancellationToken);

        if (e == null)
        {
            e = new ProjectEquipmentItem { Id = dto.Id ?? Guid.NewGuid(), ProjectId = dto.ProjectId };
            _db.ProjectEquipmentItems.Add(e);
        }

        e.Tag = dto.Tag.Trim();
        e.Name = dto.Name.Trim();
        e.HazardClass = ParseEnum(dto.HazardClass, HazardClass.Other);
        e.Zone = ParseEnum(dto.Zone, EquipmentZone.Process);
        e.Shape = ParseEnum(dto.Shape, EquipmentShape.Rectangle);
        e.WidthM = ToDecimal(dto.WidthM);
        e.LengthM = ToDecimal(dto.LengthM);
        e.DiameterM = ToDecimal(dto.DiameterM);
        e.HeightM = ToDecimal(dto.HeightM);
        e.SizeSource = ParseEnum(dto.SizeSource, SizeSource.Standard);
        e.Quantity = Math.Max(1, dto.Quantity);
        e.IsEnabled = dto.IsEnabled;
        e.IsCustom = dto.IsCustom;
        e.AllowRotation = dto.AllowRotation;
        e.NeedsRoadAccess = dto.NeedsRoadAccess;

        await _db.SaveChangesAsync(cancellationToken);

        dto.Id = e.Id;
        return dto;
    }

    public async Task DeleteEquipmentItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = await _db.ProjectEquipmentItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e == null) return;
        _db.ProjectEquipmentItems.Remove(e);
        await _db.SaveChangesAsync(cancellationToken);
    }
    public async Task<int> ImportEquipmentFromRunAsync(
    Guid projectId,
    Guid runId,
    CancellationToken cancellationToken = default)
    {
        if (await _db.ProjectEquipmentItems.AnyAsync(e => e.ProjectId == projectId, cancellationToken))
            return 0;

        var digester = await _db.SizedEquipment.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.SimulationRunId == runId && s.EquipmentType == "PRIMARY_DIGESTER_CSTR", cancellationToken);

        var sizedRows = await _db.SizedEquipment.AsNoTracking()
            .Where(s => s.SimulationRunId == runId)
            .ToListAsync(cancellationToken);



        var plan = new (string Key, int Qty)[]
        {
            ("digester", Math.Max(1, digester?.Quantity ?? 1)),
            ("mixing_tank", 2), ("feed_tank", 1), ("digestate_tank", 2),
            ("pump_house_5x5", 2), ("pump_house_9x6", 1), ("pump_house_5_5x5", 2),
            ("process_water_tank", 1), ("process_water_pump_house", 1), ("sls_building", 1),
            ("digestate_lagoon", 1), ("windrow_yard", 1), ("feedstock_area", 1), ("manure_bagging", 1),
            ("scrubber_upgrading", 1), ("compressor_filling", 1), ("flare", 1), ("hot_water_generator", 1),
            ("mcc_room", 1), ("transformer_dg", 1), ("security", 1),
            ("fire_water_tank", 1), ("fire_water_pump_house", 1),
            ("weigh_bridge", 1), ("admin_building", 1), ("parking", 1)
        };

        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int added = 0;

        foreach (var (key, qty) in plan)
        {
            var f = StandardFootprints.Find(key);
            if (f == null) continue;

            counters[f.TagPrefix] = counters.GetValueOrDefault(f.TagPrefix) + 1;

            bool isCircle = f.Shape == EquipmentShape.Circle;
            double? simDia = key == "digester" ? ReadDiameter(digester?.GeometryParamsJson) : null;
            double dia = simDia ?? f.DiameterM;

            var sizedType = key switch
            {
                "digester" => "PRIMARY_DIGESTER_CSTR",
                "mixing_tank" => "SLURRY_MIX_TANK",
                "feed_tank" => "FEED_TANK",
                "digestate_tank" => "DIGESTATE_STORAGE_TANK",
                _ => null
            };
            double? simHeight = sizedType == null
                ? null
                : ReadNumber(sizedRows.FirstOrDefault(s => s.EquipmentType == sizedType)?.GeometryParamsJson, "totalHeightM");

            _db.ProjectEquipmentItems.Add(new ProjectEquipmentItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Tag = $"{f.TagPrefix}-{counters[f.TagPrefix]:00}",
                Name = f.Name,
                HazardClass = f.HazardClass,
                Zone = f.Zone,
                Shape = f.Shape,
                WidthM = isCircle ? null : (decimal)f.WidthM,
                LengthM = isCircle ? null : (decimal)f.LengthM,
                DiameterM = isCircle ? (decimal)dia : null,
                HeightM = simHeight.HasValue ? (decimal)simHeight.Value : StandardHeightM(f.TagPrefix),
                SizeSource = simDia.HasValue ? SizeSource.Simulation : SizeSource.Standard,
                Quantity = qty,
                IsEnabled = true,
                IsCustom = false,
                AllowRotation = true,
                NeedsRoadAccess = f.NeedsRoadAccess
            });
            added++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return added;
    }

    private static decimal? StandardHeightM(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var prefix = tag.Split('-')[0].Trim().ToUpperInvariant();

        return prefix switch
        {
            "AD" => 12.0m,
            "MT" => 4.0m,
            "FT" => 5.0m,
            "DT" => 6.0m,
            "PH" => 4.0m,
            "PWT" => 4.0m,
            "PWP" => 4.0m,
            "SLS" => 6.0m,
            "LG" => 3.0m,
            "WY" => 2.0m,
            "FS" => 3.0m,
            "FB" => 3.0m,
            "DY" => 1.0m,
            "MB" => 5.0m,
            "SU" => 6.0m,
            "CF" => 5.0m,
            "FL" => 12.0m,
            "HWG" => 5.0m,
            "MCC" => 4.0m,
            "TR" => 4.0m,
            "SEC" => 3.0m,
            "FWT" => 5.0m,
            "FWP" => 4.0m,
            "WB" => 0.5m,
            "ADM" => 4.0m,
            "PK" => 0.5m,
            _ => null
        };
    }

    private static double? ReadDiameter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("diameterM", out var p) && p.TryGetDouble(out var d) && d > 0 ? d : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private static double? ReadNumber(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var p) && p.TryGetDouble(out var d) && d > 0 ? d : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<PointDto> ReadPoints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var arr = JsonSerializer.Deserialize<List<double[]>>(json, Json) ?? new();
            return arr.Where(a => a.Length >= 2).Select(a => new PointDto { X = a[0], Y = a[1] }).ToList();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) ? result : fallback;

    private static double? ToDouble(decimal? v) => v.HasValue ? (double)v.Value : null;

    private static decimal? ToDecimal(double? v) => v.HasValue ? (decimal)v.Value : null;
}