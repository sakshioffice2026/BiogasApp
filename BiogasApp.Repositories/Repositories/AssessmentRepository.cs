using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Layout.Geometry;
using BiogasApp.Layout.Sizing;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class AssessmentRepository : IAssessmentRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Standard equipment set (Valor 12-TPD layout): footprint key, tag, quantity
    private static readonly (string Key, string Tag, int Qty)[] StandardSet =
    {
        ("digester", "AD-01", 4),
        ("mixing_tank", "MT-01", 2),
        ("feed_tank", "FT-01", 1),
        ("digestate_tank", "DT-01", 2),
        ("pump_house_5x5", "PH-01", 1),
        ("pump_house_9x6", "PH-02", 1),
        ("pump_house_5x5", "PH-03", 1),
        ("pump_house_5_5x5", "PH-04", 2),
        ("process_water_tank", "PWT-01", 1),
        ("process_water_pump_house", "PWP-01", 1),
        ("sls_building", "SLS-01", 1),
        ("digestate_lagoon", "LG-01", 1),
        ("feedstock_bunkers", "FB-01", 1),
        ("sun_drying_yard", "DY-01", 1),
        ("manure_bagging", "MB-01", 1),
        ("scrubber_upgrading", "SU-01", 1),
        ("compressor_filling", "CF-01", 1),
        ("flare", "FL-01", 1),
        ("hot_water_generator", "HWG-01", 1),
        ("mcc_room", "MCC-01", 1),
        ("transformer_dg", "TR-01", 1),
        ("security", "SEC-01", 1),
        ("fire_water_tank", "FWT-01", 1),
        ("fire_water_pump_house", "FWP-01", 1),
        ("weigh_bridge", "WB-01", 1),
        ("admin_building", "ADM-01", 1),
        ("parking", "PK-01", 1)
    };

    private static readonly ReceptorType[] SensitiveTypes =
    {
        ReceptorType.School, ReceptorType.Hospital, ReceptorType.House
    };

    private readonly ApplicationDbContext _db;

    public AssessmentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    // Stored in assessment_layouts.warnings_json
    private sealed class Payload
    {
        public List<LayoutWarningDto> Warnings { get; set; } = new();
        public List<LayoutRoadDto> Roads { get; set; } = new();
        public PointDto Gate { get; set; } = new();
        public Dictionary<string, double[]> Sizes { get; set; } = new();
    }

    // ------------------------------------------------------------------ assessments

    public async Task<List<AssessmentListItemDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SiteAssessments.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var receptorTypes = await _db.AssessmentReceptors.AsNoTracking()
            .Select(r => new { r.AssessmentId, r.Type })
            .ToListAsync(cancellationToken);

        var layoutCounts = await _db.AssessmentLayouts.AsNoTracking()
            .GroupBy(l => l.AssessmentId)
            .Select(g => new { AssessmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new AssessmentListItemDto
        {
            Id = a.Id,
            Name = a.Name,
            EnquiryId = a.EnquiryId,
            ProjectId = a.ProjectId,
            Status = a.Status.ToString(),
            CapacityTpd = (double)a.CapacityTpd,
            Latitude = (double)a.Latitude,
            Longitude = (double)a.Longitude,
            PlotAreaM2 = (double)a.PlotAreaM2,
            ReceptorCount = receptorTypes.Count(r => r.AssessmentId == a.Id),
            SensitiveReceptorCount = receptorTypes.Count(r => r.AssessmentId == a.Id && SensitiveTypes.Contains(r.Type)),
            LayoutVersionCount = layoutCounts.FirstOrDefault(c => c.AssessmentId == a.Id)?.Count ?? 0,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        }).ToList();
    }

    public async Task<AssessmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.SiteAssessments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a == null) return null;

        var corners = ReadPoints(a.PlotCornersJson);
        double lat = (double)a.Latitude;
        double lon = (double)a.Longitude;
        var geo = new GeoProjection(lat, lon);

        var site = new SiteDto
        {
            ProjectId = a.Id,
            Latitude = lat,
            Longitude = lon,
            InputMode = "Typed",
            PlotCorners = corners,
            PlotCornersLatLon = corners.Select(c =>
            {
                var ll = geo.ToLatLon(new Point2D(c.X, c.Y));
                return new LatLonDto { Lat = ll.Lat, Lon = ll.Lon };
            }).ToList(),
            PlotAreaM2 = (double)a.PlotAreaM2,
            GateEdgeIndex = a.GateEdgeIndex,
            GateOffsetM = (double)a.GateOffsetM,
            NorthAngleDeg = (double)a.NorthAngleDeg,
            WindFromDeg = ToDouble(a.WindFromDeg),
            SlopeDirectionDeg = ToDouble(a.SlopeDirectionDeg),
            PowerSideDeg = ToDouble(a.PowerSideDeg),
            GasOfftakeSideDeg = ToDouble(a.GasOfftakeSideDeg),
            Notes = a.Notes
        };

        var receptors = await _db.AssessmentReceptors.AsNoTracking()
            .Where(r => r.AssessmentId == id).ToListAsync(cancellationToken);
        site.Receptors = receptors.Select(r => new ReceptorDto
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

        var zones = await _db.AssessmentNoBuildZones.AsNoTracking()
            .Where(z => z.AssessmentId == id).ToListAsync(cancellationToken);
        site.NoBuildZones = zones.Select(z => new NoBuildZoneDto
        {
            Id = z.Id,
            Reason = z.Reason.ToString(),
            Polygon = ReadPoints(z.PolygonJson),
            Notes = z.Notes
        }).ToList();

        return new AssessmentDto
        {
            Id = a.Id,
            Name = a.Name,
            EnquiryId = a.EnquiryId,
            ProjectId = a.ProjectId,
            Status = a.Status.ToString(),
            CapacityTpd = (double)a.CapacityTpd,
            Notes = a.Notes,
            CreatedBy = a.CreatedBy,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            Site = site,
            Equipment = await GetEquipmentAsync(id, cancellationToken)
        };
    }

    /// <exception cref="InvalidOperationException">Name missing or assessment already converted.</exception>
    public async Task<AssessmentDto> SaveAsync(AssessmentDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Enter an assessment name.");

        var s = dto.Site;
        var geo = new GeoProjection(s.Latitude, s.Longitude);

        List<PointDto> corners = s.InputMode switch
        {
            "Map" => s.PlotCornersLatLon
                .Select(c => geo.ToMetres(c.Lat, c.Lon))
                .Select(p => new PointDto { X = p.X, Y = p.Y }).ToList(),
            "Rectangle" when s.RectLengthM > 0 && s.RectWidthM > 0 => new List<PointDto>
            {
                new() { X = 0, Y = 0 },
                new() { X = s.RectLengthM!.Value, Y = 0 },
                new() { X = s.RectLengthM.Value, Y = s.RectWidthM!.Value },
                new() { X = 0, Y = s.RectWidthM.Value }
            },
            _ => s.PlotCorners
        };

        var poly = corners.Select(c => new Point2D(c.X, c.Y)).ToList();
        double area = poly.Count >= 3 ? PolygonOps.Area(poly) : 0;

        bool isNew = !dto.Id.HasValue;
        SiteAssessment? a = null;

        if (!isNew)
            a = await _db.SiteAssessments.FirstOrDefaultAsync(x => x.Id == dto.Id!.Value, cancellationToken);

        if (a != null && a.Status == AssessmentStatus.Converted)
            throw new InvalidOperationException("This assessment is already converted to a project and can no longer be edited.");

        if (a == null)
        {
            isNew = true;
            a = new SiteAssessment
            {
                Id = dto.Id ?? Guid.NewGuid(),
                EnquiryId = dto.EnquiryId,
                CreatedBy = dto.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                Status = AssessmentStatus.Draft
            };
            _db.SiteAssessments.Add(a);
        }

        a.Name = dto.Name.Trim();
        a.EnquiryId = dto.EnquiryId;
        a.CapacityTpd = (decimal)Math.Max(0, dto.CapacityTpd);
        a.Notes = dto.Notes;
        a.Latitude = (decimal)s.Latitude;
        a.Longitude = (decimal)s.Longitude;
        a.PlotCornersJson = JsonSerializer.Serialize(corners.Select(c => new[] { c.X, c.Y }));
        a.PlotAreaM2 = (decimal)area;
        a.GateEdgeIndex = s.GateEdgeIndex;
        a.GateOffsetM = (decimal)s.GateOffsetM;
        a.NorthAngleDeg = (decimal)s.NorthAngleDeg;
        a.WindFromDeg = ToDecimal(s.WindFromDeg);
        a.SlopeDirectionDeg = ToDecimal(s.SlopeDirectionDeg);
        a.PowerSideDeg = ToDecimal(s.PowerSideDeg);
        a.GasOfftakeSideDeg = ToDecimal(s.GasOfftakeSideDeg);
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await SaveReceptorsAsync(a.Id, s.Receptors, cancellationToken);
        await SaveNoBuildZonesAsync(a.Id, s.NoBuildZones, cancellationToken);

        if (isNew)
            await EnsureStandardEquipmentAsync(a.Id, cancellationToken);

        return (await GetAsync(a.Id, cancellationToken))!;
    }

    /// <exception cref="InvalidOperationException">Assessment is already converted.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.SiteAssessments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a == null) return;

        if (a.Status == AssessmentStatus.Converted)
            throw new InvalidOperationException("A converted assessment cannot be deleted.");

        _db.SiteAssessments.Remove(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ receptors and no-build zones

    public async Task SaveReceptorsAsync(
        Guid assessmentId,
        IEnumerable<ReceptorDto> receptors,
        CancellationToken cancellationToken = default)
    {
        var site = await _db.SiteAssessments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);

        var old = await _db.AssessmentReceptors
            .Where(r => r.AssessmentId == assessmentId).ToListAsync(cancellationToken);
        _db.AssessmentReceptors.RemoveRange(old);

        foreach (var r in receptors)
        {
            double dist = r.DistanceFromPlotM;
            if (dist <= 0 && site != null)
                dist = GeoProjection.HaversineMetres((double)site.Latitude, (double)site.Longitude, r.Latitude, r.Longitude);

            _db.AssessmentReceptors.Add(new AssessmentReceptor
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
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
        Guid assessmentId,
        IEnumerable<NoBuildZoneDto> zones,
        CancellationToken cancellationToken = default)
    {
        var old = await _db.AssessmentNoBuildZones
            .Where(z => z.AssessmentId == assessmentId).ToListAsync(cancellationToken);
        _db.AssessmentNoBuildZones.RemoveRange(old);

        foreach (var z in zones.Where(z => z.Polygon.Count >= 3))
        {
            _db.AssessmentNoBuildZones.Add(new AssessmentNoBuildZone
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Reason = ParseEnum(z.Reason, NoBuildReason.Other),
                PolygonJson = JsonSerializer.Serialize(z.Polygon.Select(p => new[] { p.X, p.Y })),
                Notes = z.Notes
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ equipment

    public async Task<List<EquipmentItemDto>> GetEquipmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        var items = await _db.AssessmentEquipmentItems.AsNoTracking()
            .Where(e => e.AssessmentId == assessmentId)
            .OrderBy(e => e.Tag)
            .ToListAsync(cancellationToken);

        return items.Select(e => new EquipmentItemDto
        {
            Id = e.Id,
            ProjectId = e.AssessmentId,
            Tag = e.Tag,
            Name = e.Name,
            HazardClass = e.HazardClass.ToString(),
            Zone = e.Zone.ToString(),
            Shape = e.Shape.ToString(),
            WidthM = ToDouble(e.WidthM),
            LengthM = ToDouble(e.LengthM),
            DiameterM = ToDouble(e.DiameterM),
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
        AssessmentEquipmentItem? e = null;
        if (dto.Id.HasValue)
            e = await _db.AssessmentEquipmentItems.FirstOrDefaultAsync(x => x.Id == dto.Id.Value, cancellationToken);

        if (e == null)
        {
            e = new AssessmentEquipmentItem { Id = dto.Id ?? Guid.NewGuid(), AssessmentId = dto.ProjectId };
            _db.AssessmentEquipmentItems.Add(e);
        }

        e.Tag = dto.Tag.Trim();
        e.Name = dto.Name.Trim();
        e.HazardClass = ParseEnum(dto.HazardClass, HazardClass.Other);
        e.Zone = ParseEnum(dto.Zone, EquipmentZone.Process);
        e.Shape = ParseEnum(dto.Shape, EquipmentShape.Rectangle);
        e.WidthM = ToDecimal(dto.WidthM);
        e.LengthM = ToDecimal(dto.LengthM);
        e.DiameterM = ToDecimal(dto.DiameterM);
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
        var e = await _db.AssessmentEquipmentItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e == null) return;

        // Saved layout versions that placed this item lose that placement
        var placed = await _db.AssessmentLayoutItems
            .Where(i => i.EquipmentItemId == id).ToListAsync(cancellationToken);
        _db.AssessmentLayoutItems.RemoveRange(placed);

        _db.AssessmentEquipmentItems.Remove(e);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureStandardEquipmentAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        bool exists = await _db.SiteAssessments.AnyAsync(x => x.Id == assessmentId, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException($"Assessment {assessmentId} was not found.");

        bool hasItems = await _db.AssessmentEquipmentItems.AnyAsync(e => e.AssessmentId == assessmentId, cancellationToken);
        if (hasItems) return;

        foreach (var (key, tag, qty) in StandardSet)
        {
            var std = StandardFootprints.Find(key);
            if (std == null) continue;

            bool circle = std.Shape == EquipmentShape.Circle;

            _db.AssessmentEquipmentItems.Add(new AssessmentEquipmentItem
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Tag = tag,
                Name = std.Name,
                HazardClass = std.HazardClass,
                Zone = std.Zone,
                Shape = std.Shape,
                WidthM = circle ? null : (decimal)std.WidthM,
                LengthM = circle ? null : (decimal)std.LengthM,
                DiameterM = circle ? (decimal)std.DiameterM : null,
                SizeSource = SizeSource.Standard,
                Quantity = qty,
                IsEnabled = true,
                IsCustom = false,
                AllowRotation = true,
                NeedsRoadAccess = std.NeedsRoadAccess
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ layouts

    public async Task<List<LayoutDto>> GetLayoutVersionsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var layouts = await _db.AssessmentLayouts.AsNoTracking()
            .Where(l => l.AssessmentId == assessmentId)
            .OrderByDescending(l => l.VersionNo)
            .ToListAsync(cancellationToken);

        return layouts.Select(l => ToLayoutDto(l, includeItems: false)).ToList();
    }

    public async Task<LayoutDto?> GetLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        var l = await _db.AssessmentLayouts.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.EquipmentItem)
            .FirstOrDefaultAsync(x => x.Id == layoutId, cancellationToken);

        return l == null ? null : ToLayoutDto(l, includeItems: true);
    }

    public async Task<LayoutDto?> GetLatestLayoutAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var l = await _db.AssessmentLayouts.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.EquipmentItem)
            .Where(x => x.AssessmentId == assessmentId)
            .OrderByDescending(x => x.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return l == null ? null : ToLayoutDto(l, includeItems: true);
    }

    public async Task<LayoutDto> SaveLayoutAsync(LayoutDto dto, CancellationToken cancellationToken = default)
    {
        int lastVersion = await _db.AssessmentLayouts
            .Where(l => l.AssessmentId == dto.ProjectId)
            .Select(l => (int?)l.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        var payload = new Payload
        {
            Warnings = dto.Warnings,
            Roads = dto.Roads,
            Gate = dto.GatePoint,
            Sizes = dto.Items.ToDictionary(
                i => SizeKey(i.EquipmentItemId, i.InstanceNo),
                i => new[] { i.WidthM, i.LengthM },
                StringComparer.OrdinalIgnoreCase)
        };

        var layout = new AssessmentLayout
        {
            Id = Guid.NewGuid(),
            AssessmentId = dto.ProjectId,
            VersionNo = lastVersion + 1,
            Origin = Enum.TryParse<LayoutOrigin>(dto.Origin, true, out var origin) ? origin : LayoutOrigin.Auto,
            RuleSetId = dto.RuleSetId,
            RuleSetVersion = dto.RuleSetVersion,
            WarningsJson = JsonSerializer.Serialize(payload),
            CreatedBy = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var i in dto.Items)
        {
            layout.Items.Add(new AssessmentLayoutItem
            {
                Id = Guid.NewGuid(),
                AssessmentLayoutId = layout.Id,
                EquipmentItemId = i.EquipmentItemId,
                InstanceNo = i.InstanceNo,
                X = (decimal)i.X,
                Y = (decimal)i.Y,
                RotationDeg = (decimal)i.RotationDeg,
                IsLocked = i.IsLocked
            });
        }

        _db.AssessmentLayouts.Add(layout);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetLayoutAsync(layout.Id, cancellationToken))!;
    }

    public async Task DeleteLayoutAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        var l = await _db.AssessmentLayouts.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == layoutId, cancellationToken);
        if (l == null) return;

        _db.AssessmentLayouts.Remove(l);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ convert to project

    /// <exception cref="InvalidOperationException">Assessment missing, already converted, or plot not entered.</exception>
    public async Task<Guid> ConvertToProjectAsync(ConvertAssessmentDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
            throw new InvalidOperationException("Enter a project name.");

        var a = await _db.SiteAssessments
            .Include(x => x.Receptors)
            .Include(x => x.NoBuildZones)
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.Id == request.AssessmentId, cancellationToken)
            ?? throw new InvalidOperationException("The assessment was not found.");

        if (a.Status == AssessmentStatus.Converted)
            throw new InvalidOperationException("This assessment is already converted to a project.");

        if (ReadPoints(a.PlotCornersJson).Count < 3)
            throw new InvalidOperationException("Enter the plot before converting to a project.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.ProjectName.Trim(),
            LocationCountry = string.IsNullOrWhiteSpace(request.LocationCountry) ? "India" : request.LocationCountry.Trim(),
            RegulatoryStandard = ParseEnum(request.RegulatoryStandard, RegulatoryStandard.PesoIndia),
            AmbientTempMinC = request.AmbientTempMinC,
            AmbientTempMaxC = request.AmbientTempMaxC,
            DigesterRegime = ParseEnum(request.DigesterRegime, DigesterRegime.Mesophilic),
            UpgradingTech = ParseEnum(request.UpgradingTech, UpgradingTech.Psa),
            Status = ProjectStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);

        _db.ProjectSites.Add(new ProjectSite
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            PlotCornersJson = a.PlotCornersJson,
            PlotAreaM2 = a.PlotAreaM2,
            GateEdgeIndex = a.GateEdgeIndex,
            GateOffsetM = a.GateOffsetM,
            NorthAngleDeg = a.NorthAngleDeg,
            WindFromDeg = a.WindFromDeg,
            SlopeDirectionDeg = a.SlopeDirectionDeg,
            PowerSideDeg = a.PowerSideDeg,
            GasOfftakeSideDeg = a.GasOfftakeSideDeg,
            Notes = a.Notes,
            UpdatedAt = DateTime.UtcNow
        });

        foreach (var r in a.Receptors)
        {
            _db.SiteReceptors.Add(new SiteReceptor
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Type = r.Type,
                Name = r.Name,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                DistanceFromPlotM = r.DistanceFromPlotM,
                Source = r.Source,
                IsConfirmed = r.IsConfirmed
            });
        }

        foreach (var z in a.NoBuildZones)
        {
            _db.NoBuildZones.Add(new NoBuildZone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Reason = z.Reason,
                PolygonJson = z.PolygonJson,
                Notes = z.Notes
            });
        }

        foreach (var e in a.Equipment)
        {
            _db.ProjectEquipmentItems.Add(new ProjectEquipmentItem
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Tag = e.Tag,
                Name = e.Name,
                HazardClass = e.HazardClass,
                Zone = e.Zone,
                Shape = e.Shape,
                WidthM = e.WidthM,
                LengthM = e.LengthM,
                DiameterM = e.DiameterM,
                SizeSource = e.SizeSource,
                Quantity = e.Quantity,
                IsEnabled = e.IsEnabled,
                IsCustom = e.IsCustom,
                AllowRotation = e.AllowRotation,
                NeedsRoadAccess = e.NeedsRoadAccess
            });
        }

        a.Status = AssessmentStatus.Converted;
        a.ProjectId = project.Id;
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return project.Id;
    }

    // ------------------------------------------------------------------ helpers

    private static LayoutDto ToLayoutDto(AssessmentLayout l, bool includeItems)
    {
        var payload = ReadPayload(l.WarningsJson);

        var dto = new LayoutDto
        {
            Id = l.Id,
            ProjectId = l.AssessmentId,
            SimulationRunId = Guid.Empty,
            VersionNo = l.VersionNo,
            Origin = l.Origin.ToString(),
            RuleSetId = l.RuleSetId,
            RuleSetVersion = l.RuleSetVersion,
            IsDraft = true,
            GatePoint = payload.Gate,
            CreatedBy = l.CreatedBy,
            CreatedAt = l.CreatedAt,
            Warnings = payload.Warnings,
            Roads = payload.Roads
        };

        if (!includeItems) return dto;

        dto.Items = l.Items
            .OrderBy(i => i.EquipmentItem?.Tag)
            .ThenBy(i => i.InstanceNo)
            .Select(i =>
            {
                var e = i.EquipmentItem;
                double w = (double)(e?.WidthM ?? e?.DiameterM ?? 0m);
                double len = (double)(e?.LengthM ?? e?.DiameterM ?? 0m);

                if (payload.Sizes.TryGetValue(SizeKey(i.EquipmentItemId, i.InstanceNo), out var s) && s.Length >= 2)
                {
                    w = s[0];
                    len = s[1];
                }

                return new LayoutItemDto
                {
                    EquipmentItemId = i.EquipmentItemId,
                    InstanceNo = i.InstanceNo,
                    Tag = e?.Tag ?? string.Empty,
                    Name = e?.Name ?? string.Empty,
                    Zone = e?.Zone.ToString() ?? string.Empty,
                    Shape = e?.Shape.ToString() ?? string.Empty,
                    X = (double)i.X,
                    Y = (double)i.Y,
                    RotationDeg = (double)i.RotationDeg,
                    WidthM = w,
                    LengthM = len,
                    IsLocked = i.IsLocked
                };
            }).ToList();

        return dto;
    }

    private static Payload ReadPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Payload();

        try
        {
            return JsonSerializer.Deserialize<Payload>(json, Json) ?? new Payload();
        }
        catch (JsonException)
        {
            return new Payload();
        }
    }

    private static string SizeKey(Guid equipmentId, int instanceNo) => $"{equipmentId}:{instanceNo}";

    private static List<PointDto> ReadPoints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var arr = JsonSerializer.Deserialize<List<double[]>>(json, Json) ?? new();
            return arr.Where(p => p.Length >= 2).Select(p => new PointDto { X = p[0], Y = p[1] }).ToList();
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
