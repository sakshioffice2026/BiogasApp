using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class SiteLayoutRepository : ISiteLayoutRepository
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _db;

    public SiteLayoutRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    // Stored in site_layouts.warnings_json
    private sealed class Payload
    {
        public List<LayoutWarningDto> Warnings { get; set; } = new();
        public List<LayoutRoadDto> Roads { get; set; } = new();
        public PointDto Gate { get; set; } = new();
        public Dictionary<string, double[]> Sizes { get; set; } = new();
    }

    public async Task<List<LayoutDto>> GetVersionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var layouts = await _db.SiteLayouts.AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .OrderByDescending(l => l.VersionNo)
            .ToListAsync(cancellationToken);

        return layouts.Select(l => ToDto(l, includeItems: false)).ToList();
    }

    public async Task<LayoutDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await _db.SiteLayouts.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.EquipmentItem)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return l == null ? null : ToDto(l, includeItems: true);
    }

    public async Task<LayoutDto?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var l = await _db.SiteLayouts.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.EquipmentItem)
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return l == null ? null : ToDto(l, includeItems: true);
    }

    public async Task<LayoutDto> SaveAsync(LayoutDto dto, CancellationToken cancellationToken = default)
    {
        int lastVersion = await _db.SiteLayouts
            .Where(l => l.ProjectId == dto.ProjectId)
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

        var layout = new SiteLayout
        {
            Id = Guid.NewGuid(),
            SimulationRunId = dto.SimulationRunId,
            ProjectId = dto.ProjectId,
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
            layout.Items.Add(new SiteLayoutItem
            {
                Id = Guid.NewGuid(),
                SiteLayoutId = layout.Id,
                EquipmentItemId = i.EquipmentItemId,
                InstanceNo = i.InstanceNo,
                X = (decimal)i.X,
                Y = (decimal)i.Y,
                RotationDeg = (decimal)i.RotationDeg,
                IsLocked = i.IsLocked
            });
        }

        _db.SiteLayouts.Add(layout);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(layout.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await _db.SiteLayouts.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (l == null) return;

        // Detach any CAD job that points at this layout
        var jobs = await _db.CadJobs.Where(j => j.SiteLayoutId == id).ToListAsync(cancellationToken);
        foreach (var j in jobs) j.SiteLayoutId = null;

        _db.SiteLayouts.Remove(l);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static LayoutDto ToDto(SiteLayout l, bool includeItems)
    {
        var payload = ReadPayload(l.WarningsJson);

        var dto = new LayoutDto
        {
            Id = l.Id,
            ProjectId = l.ProjectId,
            SimulationRunId = l.SimulationRunId,
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
            try
            {
                // Older format: plain list of warnings
                var list = JsonSerializer.Deserialize<List<LayoutWarningDto>>(json, Json) ?? new();
                return new Payload { Warnings = list };
            }
            catch (JsonException)
            {
                return new Payload();
            }
        }
    }

    private static string SizeKey(Guid equipmentId, int instanceNo) => $"{equipmentId}:{instanceNo}";
}