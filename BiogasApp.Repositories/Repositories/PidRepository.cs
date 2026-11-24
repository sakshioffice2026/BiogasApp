using System.Text.Json;
using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class PidRepository : IPidRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;

    public PidRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetProjectNameAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PidRunOptionDto>> GetRunOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _db.SimulationRuns.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RunVersion)
            .Select(r => new PidRunOptionDto
            {
                SimulationRunId = r.Id,
                RunVersion = r.RunVersion,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProjectEquipmentItem>> GetProjectEquipmentAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _db.ProjectEquipmentItems.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Tag)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SizedEquipment>> GetSizedEquipmentAsync(Guid simulationRunId, CancellationToken cancellationToken = default)
    {
        return await _db.SizedEquipment.AsNoTracking()
            .Where(s => s.SimulationRunId == simulationRunId)
            .OrderBy(s => s.EquipmentTag)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PidDiagramDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _db.PidDiagrams.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new PidDiagramDto
            {
                Id = d.Id,
                ProjectId = d.ProjectId,
                SimulationRunId = d.SimulationRunId,
                DrawingNo = d.DrawingNo,
                Title = d.Title,
                RevisionNo = d.RevisionNo,
                Status = d.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PidDiagramDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _db.PidDiagrams.AsNoTracking()
            .Include(x => x.Nodes)
            .Include(x => x.Lines)
            .Include(x => x.Valves)
            .Include(x => x.Instruments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return d == null ? null : ToDto(d);
    }

    public async Task<PidDiagramDto?> GetLatestAsync(Guid projectId, Guid simulationRunId, CancellationToken cancellationToken = default)
    {
        var id = await _db.PidDiagrams.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.SimulationRunId == simulationRunId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id.HasValue ? await GetByIdAsync(id.Value, cancellationToken) : null;
    }

    public async Task<Guid> CreateAsync(PidDiagram diagram, string? user, CancellationToken cancellationToken = default)
    {
        var count = await _db.PidDiagrams.CountAsync(x => x.ProjectId == diagram.ProjectId, cancellationToken);
        diagram.DrawingNo = $"PID-{count + 1:000}";
        diagram.CreatedBy ??= user;

        diagram.Revisions.Add(new PidRevision
        {
            Id = Guid.NewGuid(),
            PidDiagramId = diagram.Id,
            RevisionNo = diagram.RevisionNo,
            Status = PidApprovalStatus.Draft,
            Remarks = "Auto-generated",
            SnapshotJson = JsonSerializer.Serialize(ToDto(diagram), Json),
            ChangedBy = user,
            ChangedAt = DateTime.UtcNow
        });

        _db.PidDiagrams.Add(diagram);
        await _db.SaveChangesAsync(cancellationToken);
        return diagram.Id;
    }

    public async Task<PidDiagramDto?> SaveAsync(PidDiagramDto dto, string? user, CancellationToken cancellationToken = default)
    {
        var d = await _db.PidDiagrams
            .Include(x => x.Nodes)
            .Include(x => x.Lines)
            .Include(x => x.Valves)
            .Include(x => x.Instruments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == dto.Id, cancellationToken);

        if (d == null) return null;

        if (d.Status == PidApprovalStatus.Approved)
            throw new InvalidOperationException("Approved diagram is read-only. Create a new revision to edit.");

        if (!string.IsNullOrWhiteSpace(dto.Title)) d.Title = dto.Title.Trim();
        d.UpdatedAt = DateTime.UtcNow;

        // ---- nodes ----
        var nodeMap = d.Nodes.ToDictionary(x => x.Id);
        var keepNodes = new HashSet<Guid>();
        foreach (var nd in dto.Nodes)
        {
            var nodeId = nd.Id == Guid.Empty ? Guid.NewGuid() : nd.Id;
            keepNodes.Add(nodeId);

            if (!nodeMap.TryGetValue(nodeId, out var node))
            {
                node = new PidEquipmentNode { Id = nodeId, PidDiagramId = d.Id };
                _db.PidEquipmentNodes.Add(node);
                nodeMap[nodeId] = node;
            }

            node.EquipmentItemId = nd.EquipmentItemId;
            node.SizedEquipmentId = nd.SizedEquipmentId;
            node.Tag = nd.Tag;
            node.Name = nd.Name;
            node.SymbolType = nd.SymbolType;
            node.StageNo = nd.StageNo;
            node.InstanceNo = nd.InstanceNo;
            node.X = nd.X;
            node.Y = nd.Y;
            node.RotationDeg = nd.RotationDeg;
            node.IsLocked = nd.IsLocked;
        }

        // ---- lines ----
        var lineMap = d.Lines.ToDictionary(x => x.Id);
        var keepLines = new HashSet<Guid>();
        foreach (var ld in dto.Lines)
        {
            if (!keepNodes.Contains(ld.FromNodeId) || !keepNodes.Contains(ld.ToNodeId)) continue;

            var lineId = ld.Id == Guid.Empty ? Guid.NewGuid() : ld.Id;
            keepLines.Add(lineId);

            if (!lineMap.TryGetValue(lineId, out var line))
            {
                line = new PidLine { Id = lineId, PidDiagramId = d.Id };
                _db.PidLines.Add(line);
                lineMap[lineId] = line;
            }

            line.LineNo = ld.LineNo;
            line.FromNodeId = ld.FromNodeId;
            line.ToNodeId = ld.ToNodeId;
            line.Medium = ld.Medium;
            line.NominalSizeMm = ld.NominalSizeMm;
            line.Material = ld.Material;
            line.IsInsulated = ld.IsInsulated;
            line.PointsJson = JsonSerializer.Serialize(ld.Points ?? new List<PidPointDto>(), Json);
            line.Notes = ld.Notes;
        }

        // ---- valves ----
        var valveMap = d.Valves.ToDictionary(x => x.Id);
        var keepValves = new HashSet<Guid>();
        foreach (var vd in dto.Valves)
        {
            if (!keepLines.Contains(vd.PidLineId)) continue;

            var valveId = vd.Id == Guid.Empty ? Guid.NewGuid() : vd.Id;
            keepValves.Add(valveId);

            if (!valveMap.TryGetValue(valveId, out var valve))
            {
                valve = new PidValve { Id = valveId, PidDiagramId = d.Id };
                _db.PidValves.Add(valve);
                valveMap[valveId] = valve;
            }

            valve.PidLineId = vd.PidLineId;
            valve.Tag = vd.Tag;
            valve.ValveType = vd.ValveType;
            valve.NominalSizeMm = vd.NominalSizeMm;
            valve.PositionOnLine = vd.PositionOnLine;
            valve.X = vd.X;
            valve.Y = vd.Y;
            valve.RotationDeg = vd.RotationDeg;
            valve.IsNormallyOpen = vd.IsNormallyOpen;
            valve.Notes = vd.Notes;
        }

        // ---- instruments ----
        var insMap = d.Instruments.ToDictionary(x => x.Id);
        var keepIns = new HashSet<Guid>();
        foreach (var idto in dto.Instruments)
        {
            var insId = idto.Id == Guid.Empty ? Guid.NewGuid() : idto.Id;
            keepIns.Add(insId);

            if (!insMap.TryGetValue(insId, out var ins))
            {
                ins = new PidInstrument { Id = insId, PidDiagramId = d.Id };
                _db.PidInstruments.Add(ins);
                insMap[insId] = ins;
            }

            ins.PidLineId = idto.PidLineId.HasValue && keepLines.Contains(idto.PidLineId.Value) ? idto.PidLineId : null;
            ins.PidEquipmentNodeId = idto.PidEquipmentNodeId.HasValue && keepNodes.Contains(idto.PidEquipmentNodeId.Value) ? idto.PidEquipmentNodeId : null;
            ins.Tag = idto.Tag;
            ins.InstrumentType = idto.InstrumentType;
            ins.Service = idto.Service;
            ins.RangeMin = idto.RangeMin;
            ins.RangeMax = idto.RangeMax;
            ins.Unit = idto.Unit;
            ins.X = idto.X;
            ins.Y = idto.Y;
            ins.Notes = idto.Notes;
        }

        // ---- removals ----
        foreach (var gone in d.Instruments.Where(x => !keepIns.Contains(x.Id)).ToList())
            _db.PidInstruments.Remove(gone);

        foreach (var gone in d.Valves.Where(x => !keepValves.Contains(x.Id)).ToList())
            _db.PidValves.Remove(gone);

        foreach (var gone in d.Lines.Where(x => !keepLines.Contains(x.Id)).ToList())
            _db.PidLines.Remove(gone);

        foreach (var gone in d.Nodes.Where(x => !keepNodes.Contains(x.Id)).ToList())
            _db.PidEquipmentNodes.Remove(gone);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(d.Id, cancellationToken);
    }

    public async Task<bool> ChangeStatusAsync(Guid id, PidApprovalStatus status, string? user, string? remarks, CancellationToken cancellationToken = default)
    {
        var d = await _db.PidDiagrams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null) return false;

        var snapshot = await GetByIdAsync(id, cancellationToken);
        if (snapshot != null) snapshot.Status = status;

        var now = DateTime.UtcNow;
        d.Status = status;
        d.UpdatedAt = now;

        if (status == PidApprovalStatus.Approved)
        {
            d.ApprovedBy = user;
            d.ApprovedAt = now;
        }
        else
        {
            d.ApprovedBy = null;
            d.ApprovedAt = null;
        }

        _db.PidRevisions.Add(new PidRevision
        {
            Id = Guid.NewGuid(),
            PidDiagramId = d.Id,
            RevisionNo = d.RevisionNo,
            Status = status,
            Remarks = remarks,
            SnapshotJson = snapshot == null ? null : JsonSerializer.Serialize(snapshot, Json),
            ChangedBy = user,
            ChangedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CreateRevisionAsync(Guid id, string? user, string? remarks, CancellationToken cancellationToken = default)
    {
        var d = await _db.PidDiagrams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null) return false;

        var snapshot = await GetByIdAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        d.RevisionNo += 1;
        d.Status = PidApprovalStatus.Draft;
        d.ApprovedBy = null;
        d.ApprovedAt = null;
        d.UpdatedAt = now;

        if (snapshot != null)
        {
            snapshot.RevisionNo = d.RevisionNo;
            snapshot.Status = PidApprovalStatus.Draft;
        }

        _db.PidRevisions.Add(new PidRevision
        {
            Id = Guid.NewGuid(),
            PidDiagramId = d.Id,
            RevisionNo = d.RevisionNo,
            Status = PidApprovalStatus.Draft,
            Remarks = remarks,
            SnapshotJson = snapshot == null ? null : JsonSerializer.Serialize(snapshot, Json),
            ChangedBy = user,
            ChangedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<PidRevisionItemDto>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.PidRevisions.AsNoTracking()
            .Where(r => r.PidDiagramId == id)
            .OrderByDescending(r => r.ChangedAt)
            .Select(r => new PidRevisionItemDto
            {
                Id = r.Id,
                RevisionNo = r.RevisionNo,
                Status = r.Status,
                Remarks = r.Remarks,
                ChangedBy = r.ChangedBy,
                ChangedAt = r.ChangedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _db.PidDiagrams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null) return;

        _db.PidDiagrams.Remove(d);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static PidDiagramDto ToDto(PidDiagram d) => new()
    {
        Id = d.Id,
        ProjectId = d.ProjectId,
        SimulationRunId = d.SimulationRunId,
        DrawingNo = d.DrawingNo,
        Title = d.Title,
        RevisionNo = d.RevisionNo,
        Status = d.Status,
        Nodes = d.Nodes.Select(n => new PidNodeDto
        {
            Id = n.Id,
            EquipmentItemId = n.EquipmentItemId,
            SizedEquipmentId = n.SizedEquipmentId,
            Tag = n.Tag,
            Name = n.Name,
            SymbolType = n.SymbolType,
            StageNo = n.StageNo,
            InstanceNo = n.InstanceNo,
            X = n.X,
            Y = n.Y,
            RotationDeg = n.RotationDeg,
            IsLocked = n.IsLocked
        }).ToList(),
        Lines = d.Lines.Select(l => new PidLineDto
        {
            Id = l.Id,
            LineNo = l.LineNo,
            FromNodeId = l.FromNodeId,
            ToNodeId = l.ToNodeId,
            Medium = l.Medium,
            NominalSizeMm = l.NominalSizeMm,
            Material = l.Material,
            IsInsulated = l.IsInsulated,
            Points = ReadPoints(l.PointsJson),
            Notes = l.Notes
        }).ToList(),
        Valves = d.Valves.Select(v => new PidValveDto
        {
            Id = v.Id,
            PidLineId = v.PidLineId,
            Tag = v.Tag,
            ValveType = v.ValveType,
            NominalSizeMm = v.NominalSizeMm,
            PositionOnLine = v.PositionOnLine,
            X = v.X,
            Y = v.Y,
            RotationDeg = v.RotationDeg,
            IsNormallyOpen = v.IsNormallyOpen,
            Notes = v.Notes
        }).ToList(),
        Instruments = d.Instruments.Select(i => new PidInstrumentDto
        {
            Id = i.Id,
            PidLineId = i.PidLineId,
            PidEquipmentNodeId = i.PidEquipmentNodeId,
            Tag = i.Tag,
            InstrumentType = i.InstrumentType,
            Service = i.Service,
            RangeMin = i.RangeMin,
            RangeMax = i.RangeMax,
            Unit = i.Unit,
            X = i.X,
            Y = i.Y,
            Notes = i.Notes
        }).ToList()
    };

    private static List<PidPointDto> ReadPoints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<PidPointDto>>(json, Json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
