using BiogasApp.Database;
using BiogasApp.Database.Entities;
using BiogasApp.Database.Enums;
using BiogasApp.Model.ViewModel;
using BiogasApp.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BiogasApp.Repositories.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly ApplicationDbContext _db;

    public ProjectRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectIndexViewModel> GetIndexAsync(
        string? search,
        ProjectStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.LocationCountry.Contains(term));
        }

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var projects = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                LocationCountry = p.LocationCountry,
                RegulatoryStandard = p.RegulatoryStandard,
                DigesterRegime = p.DigesterRegime,
                UpgradingTech = p.UpgradingTech,
                Status = p.Status,
                RunCount = p.SimulationRuns.Count,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ProjectIndexViewModel
        {
            Search = search,
            StatusFilter = status,
            Projects = projects
        };
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        if (project.Id == Guid.Empty)
            project.Id = Guid.NewGuid();

        project.Name = project.Name.Trim();
        project.LocationCountry = project.LocationCountry.Trim();
        project.CreatedAt = DateTime.UtcNow;

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return project;
    }

    /// <summary>Copies every editable field; Id and CreatedAt are never changed.</summary>
    /// <exception cref="KeyNotFoundException">Project does not exist.</exception>
    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {project.Id} was not found.");

        entity.Name = project.Name.Trim();
        entity.LocationCountry = project.LocationCountry.Trim();
        entity.RegulatoryStandard = project.RegulatoryStandard;
        entity.AmbientTempMinC = project.AmbientTempMinC;
        entity.AmbientTempMaxC = project.AmbientTempMaxC;
        entity.DigesterRegime = project.DigesterRegime;
        entity.UpgradingTech = project.UpgradingTech;
        entity.Status = project.Status;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Project does not exist.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {id} was not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Rows that block the cascade (restricted foreign keys) are removed first.
        await _db.EnquiryProposals
            .Where(x => x.ProjectId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.SiteLayoutItems
            .Where(x => _db.SiteLayouts.Any(l => l.Id == x.SiteLayoutId && l.ProjectId == id))
            .ExecuteDeleteAsync(cancellationToken);

        _db.Projects.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    /// <exception cref="KeyNotFoundException">Project does not exist.</exception>
    public async Task SetStatusAsync(Guid id, ProjectStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {id} was not found.");

        if (entity.Status == status) return;

        entity.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ProjectFeedstockMix>> GetFeedstockMixAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        _db.ProjectFeedstockMixes
            .AsNoTracking()
            .Include(m => m.Feedstock)
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.Feedstock!.Name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Replaces the project's feedstock mix with the supplied rows (matched by FeedstockId):
    /// missing rows are removed, existing rows updated, new rows added. All in one SaveChanges.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Project or a referenced feedstock does not exist.</exception>
    /// <exception cref="ArgumentException">Duplicate feedstock, or a value is out of range.</exception>
    public async Task SaveFeedstockMixAsync(
        Guid projectId,
        IEnumerable<ProjectFeedstockMix> mix,
        CancellationToken cancellationToken = default)
    {
        var incoming = mix.ToList();

        if (!await _db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken))
            throw new KeyNotFoundException($"Project {projectId} was not found.");

        ValidateMix(incoming);

        var feedstockIds = incoming.Select(m => m.FeedstockId).ToList();
        var foundCount = feedstockIds.Count == 0
            ? 0
            : await _db.Feedstocks.CountAsync(f => feedstockIds.Contains(f.Id), cancellationToken);

        if (foundCount != feedstockIds.Count)
            throw new KeyNotFoundException("One or more selected feedstocks do not exist.");

        var existing = await _db.ProjectFeedstockMixes
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        // Remove rows no longer present
        foreach (var row in existing.Where(e => !feedstockIds.Contains(e.FeedstockId)))
            _db.ProjectFeedstockMixes.Remove(row);

        foreach (var item in incoming)
        {
            var current = existing.FirstOrDefault(e => e.FeedstockId == item.FeedstockId);

            if (current is null)
            {
                _db.ProjectFeedstockMixes.Add(new ProjectFeedstockMix
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FeedstockId = item.FeedstockId,
                    DailyTonnageTpd = item.DailyTonnageTpd,
                    SeasonalAvailabilityDays = item.SeasonalAvailabilityDays,
                    CustomTsOverride = item.CustomTsOverride,
                    CustomVsOverride = item.CustomVsOverride
                });
            }
            else
            {
                current.DailyTonnageTpd = item.DailyTonnageTpd;
                current.SeasonalAvailabilityDays = item.SeasonalAvailabilityDays;
                current.CustomTsOverride = item.CustomTsOverride;
                current.CustomVsOverride = item.CustomVsOverride;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateMix(List<ProjectFeedstockMix> mix)
    {
        if (mix.GroupBy(m => m.FeedstockId).Any(g => g.Count() > 1))
            throw new ArgumentException("A feedstock can only appear once in a project mix.", nameof(mix));

        foreach (var m in mix)
        {
            if (m.DailyTonnageTpd < 0)
                throw new ArgumentException("Daily tonnage cannot be negative.", nameof(mix));

            if (m.SeasonalAvailabilityDays is < 1 or > 365)
                throw new ArgumentException("Seasonal availability must be between 1 and 365 days.", nameof(mix));

            if (m.CustomTsOverride is < 0 or > 100)
                throw new ArgumentException("TS override must be between 0 and 100.", nameof(mix));

            if (m.CustomVsOverride is < 0 or > 100)
                throw new ArgumentException("VS override must be between 0 and 100.", nameof(mix));
        }
    }
}