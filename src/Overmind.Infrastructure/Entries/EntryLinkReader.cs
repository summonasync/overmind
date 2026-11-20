using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

public sealed class EntryLinkReader
{
    private readonly OvermindDbContext _db;

    public EntryLinkReader(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BacklinkSource>> LoadBacklinkSourcesAsync(
        Guid targetEntryId,
        CancellationToken ct = default)
    {
        var query =
            from link in _db.EntryLinks
            where link.ToKind == EntryLinkKind.Entry && link.ToId == targetEntryId
            join source in _db.ContextEntries on link.FromEntryId equals source.Id
            where source.State == ContextEntry.ActiveState && source.Type != EntryType.Tombstone
            orderby source.UpdatedAt descending, source.Id
            select new BacklinkSource(source.Id, source.Title, source.Content);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<(EntryLinkKind Kind, Guid Id), string>> LoadTargetLabelsAsync(
        IReadOnlyCollection<ResolvedLink> links,
        CancellationToken ct = default)
    {
        var labels = new Dictionary<(EntryLinkKind, Guid), string>();
        if (links.Count == 0)
        {
            return labels;
        }

        var entryIds = links.Where(link => link.Kind == EntryLinkKind.Entry).Select(link => link.ToId).ToArray();
        if (entryIds.Length > 0)
        {
            var entries = await _db.ContextEntries
                .AsNoTracking()
                .Where(e => entryIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Title })
                .ToListAsync(ct);

            foreach (var entry in entries)
            {
                labels[(EntryLinkKind.Entry, entry.Id)] = entry.Title;
            }
        }

        var featureIds = links.Where(link => link.Kind == EntryLinkKind.Feature).Select(link => link.ToId).ToArray();
        if (featureIds.Length > 0)
        {
            var features = await _db.PlanningFeatures
                .AsNoTracking()
                .Where(f => featureIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Title })
                .ToListAsync(ct);

            foreach (var feature in features)
            {
                labels[(EntryLinkKind.Feature, feature.Id)] = feature.Title;
            }
        }

        var projectIds = links.Where(link => link.Kind == EntryLinkKind.Project).Select(link => link.ToId).ToArray();
        if (projectIds.Length > 0)
        {
            var projects = await _db.Projects
                .AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(ct);

            foreach (var project in projects)
            {
                labels[(EntryLinkKind.Project, project.Id)] = project.Name;
            }
        }

        return labels;
    }
}
