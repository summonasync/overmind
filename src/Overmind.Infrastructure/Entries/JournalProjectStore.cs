using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

public sealed class JournalProjectStore
{
    public const string ProjectName = "Journal";

    private readonly OvermindDbContext _db;

    public JournalProjectStore(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> FindIdAsync(CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Select(p => new { p.Id, p.Name, p.CreatedAt })
            .ToListAsync(ct);

        return ProjectNameMatch.PickDeterministic(
            projects.Select(project => (project.Id, project.Name, project.CreatedAt)),
            ProjectName);
    }

    public async Task<Guid> EnsureIdAsync(CancellationToken ct = default)
    {
        if (await FindIdAsync(ct) is { } existingId)
        {
            return existingId;
        }

        var dataSource = _db.Database.GetDbConnection().DataSource;
        var diskPath = Path.GetDirectoryName(dataSource);
        if (string.IsNullOrWhiteSpace(diskPath))
        {
            diskPath = dataSource;
        }

        var project = Project.Create(ProjectName, diskPath);
        _db.Projects.Add(project);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Entry(project).State = EntityState.Detached;
            if (await FindIdAsync(ct) is { } racedId)
            {
                return racedId;
            }

            throw;
        }

        return project.Id;
    }
}
