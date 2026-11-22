using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

public sealed class EntryLinkReader
{
    private readonly OvermindDbContext _db;
    private readonly EntryLinkTriage _triage;

    public EntryLinkReader(OvermindDbContext db, EntryLinkTriage triage)
    {
        _db = db;
        _triage = triage;
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

    public async Task<IReadOnlyList<ResolvedLink>> LoadManualOutgoingAsync(
        Guid fromEntryId,
        CancellationToken ct = default)
    {
        return
        [
            .. await _triage.Edges()
                .Where(link => link.FromEntryId == fromEntryId && link.Origin == EntryLinkOrigin.Manual)
                .OrderBy(link => link.CreatedAt)
                .ThenBy(link => link.Id)
                .Select(link => new ResolvedLink(link.ToKind, link.ToId))
                .ToListAsync(ct)
        ];
    }

    public async Task<IReadOnlyDictionary<(EntryLinkKind Kind, Guid Id), TargetLabel>> LoadTargetLabelsAsync(
        IReadOnlyCollection<ResolvedLink> links,
        CancellationToken ct = default)
    {
        var labels = new Dictionary<(EntryLinkKind, Guid), TargetLabel>();
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
                labels[(EntryLinkKind.Entry, entry.Id)] = new TargetLabel(entry.Title);
            }
        }

        var featureIds = links.Where(link => link.Kind == EntryLinkKind.Feature).Select(link => link.ToId).ToArray();
        if (featureIds.Length > 0)
        {
            var features = await _db.PlanningFeatures
                .AsNoTracking()
                .Where(f => featureIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Title, f.Slug, f.ProjectId })
                .ToListAsync(ct);

            foreach (var feature in features)
            {
                labels[(EntryLinkKind.Feature, feature.Id)] =
                    new TargetLabel(feature.Title, feature.Slug, feature.ProjectId);
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
                labels[(EntryLinkKind.Project, project.Id)] = new TargetLabel(project.Name);
            }
        }

        var taskIds = links.Where(link => link.Kind == EntryLinkKind.Task).Select(link => link.ToId).ToArray();
        if (taskIds.Length > 0)
        {
            var tasks = await _db.PlanningArtifacts
                .AsNoTracking()
                .Where(artifact => taskIds.Contains(artifact.Id) && artifact.Kind == PlanningArtifactKind.Task)
                .Select(artifact => new { artifact.Id, artifact.Brief, artifact.Order, artifact.FeatureId })
                .ToListAsync(ct);

            var hostIds = tasks.Select(task => task.FeatureId).Distinct().ToArray();
            var hosts = await _db.PlanningFeatures
                .AsNoTracking()
                .Where(feature => hostIds.Contains(feature.Id))
                .Select(feature => new { feature.Id, feature.Slug, feature.ProjectId })
                .ToListAsync(ct);

            foreach (var task in tasks)
            {
                var host = hosts.FirstOrDefault(feature => feature.Id == task.FeatureId);
                labels[(EntryLinkKind.Task, task.Id)] = new TargetLabel(
                    string.IsNullOrWhiteSpace(task.Brief) ? $"task {task.Order:D2}" : task.Brief,
                    host?.Slug,
                    host?.ProjectId);
            }
        }

        return labels;
    }
}
