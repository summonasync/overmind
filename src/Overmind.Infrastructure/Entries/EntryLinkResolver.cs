using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Entries;

public sealed class EntryLinkResolver
{
    private readonly OvermindDbContext _db;
    private readonly JournalProjectStore _journalProjects;

    public EntryLinkResolver(OvermindDbContext db, JournalProjectStore journalProjects)
    {
        _db = db;
        _journalProjects = journalProjects;
    }

    public Task<IReadOnlyList<ResolvedLink>> ResolveForWriteAsync(
        Guid sourceEntryId,
        IReadOnlyList<WikiLinkTarget> targets,
        CancellationToken ct = default) =>
        ResolveAsync(sourceEntryId, targets, ensureJournalProject: true, ct);

    public Task<IReadOnlyList<WikiLinkOutcome>> ResolveOutcomesForReadAsync(
        Guid sourceEntryId,
        IReadOnlyList<WikiLinkTarget> targets,
        CancellationToken ct = default) =>
        ResolveOutcomesAsync(sourceEntryId, targets, ensureJournalProject: false, ct);

    private async Task<IReadOnlyList<ResolvedLink>> ResolveAsync(
        Guid sourceEntryId,
        IReadOnlyList<WikiLinkTarget> targets,
        bool ensureJournalProject,
        CancellationToken ct)
    {
        var resolved = new List<ResolvedLink>();
        var seen = new HashSet<(EntryLinkKind, Guid)>();

        foreach (var outcome in await ResolveOutcomesAsync(sourceEntryId, targets, ensureJournalProject, ct))
        {
            if (outcome.ToId is not { } resolvedId || resolvedId == sourceEntryId)
            {
                continue;
            }

            if (seen.Add((outcome.Target.Kind, resolvedId)))
            {
                resolved.Add(new ResolvedLink(outcome.Target.Kind, resolvedId));
            }
        }

        return resolved;
    }

    private async Task<IReadOnlyList<WikiLinkOutcome>> ResolveOutcomesAsync(
        Guid sourceEntryId,
        IReadOnlyList<WikiLinkTarget> targets,
        bool ensureJournalProject,
        CancellationToken ct)
    {
        var outcomes = new List<WikiLinkOutcome>(targets.Count);
        ProjectIndex? projects = null;
        Dictionary<string, Guid>? journalTitles = null;

        foreach (var target in targets)
        {
            if (target.Kind == EntryLinkKind.Project && projects is null)
            {
                projects = await LoadProjectsAsync(ct);
            }

            if (target.Form == WikiLinkKeyForm.Title && journalTitles is null)
            {
                journalTitles = await LoadJournalTitlesAsync(ensureJournalProject, ct);
            }

            Guid? targetId = target.Kind switch
            {
                EntryLinkKind.Feature => await ResolveFeatureAsync(target, ct),
                EntryLinkKind.Project => projects!.Resolve(target),
                _ => ResolveTitleId(target, journalTitles)
            };

            outcomes.Add(new WikiLinkOutcome(target, targetId));
        }

        return outcomes;
    }

    private async Task<Guid?> ResolveFeatureAsync(WikiLinkTarget target, CancellationToken ct)
    {
        if (target.Form == WikiLinkKeyForm.Number
            && int.TryParse(target.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return await ResolveFeatureNumberAsync(number, target.Key, ct);
        }

        var slug = target.Key;
        var slugIds = await _db.PlanningFeatures
            .Where(f => f.Slug == slug)
            .Select(f => f.Id)
            .ToListAsync(ct);

        return slugIds.Count == 1 ? slugIds[0] : null;
    }

    private async Task<Guid?> ResolveFeatureNumberAsync(int number, string key, CancellationToken ct)
    {
        var padded = number.ToString("000", CultureInfo.InvariantCulture) + "-";
        var raw = key + "-";
        var candidates = await _db.PlanningFeatures
            .Where(f => f.Slug.StartsWith(padded) || f.Slug.StartsWith(raw))
            .Select(f => new { f.Id, f.Slug })
            .ToListAsync(ct);

        Guid? match = null;
        foreach (var candidate in candidates)
        {
            if (!FeatureNumberPrefix.TryParse(candidate.Slug, out var parsed) || parsed != number)
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = candidate.Id;
        }

        return match;
    }

    private async Task<ProjectIndex> LoadProjectsAsync(CancellationToken ct)
    {
        var rows = await _db.Projects
            .Select(p => new { p.Id, p.Name, p.CreatedAt })
            .ToListAsync(ct);

        return new ProjectIndex([.. rows.Select(row => (row.Id, row.Name, row.CreatedAt))]);
    }

    private async Task<Dictionary<string, Guid>> LoadJournalTitlesAsync(
        bool ensureProject,
        CancellationToken ct)
    {
        var byTitle = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var projectId = ensureProject
            ? await _journalProjects.EnsureIdAsync(ct)
            : await _journalProjects.FindIdAsync(ct);
        if (projectId is not { } containerId)
        {
            return byTitle;
        }

        var rows = await _db.ContextEntries
            .Where(e => e.ProjectId == containerId && e.Type == EntryType.Journal)
            .OrderBy(e => e.UpdatedAt)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.Title })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            byTitle[row.Title] = row.Id;
        }

        return byTitle;
    }

    private static Guid? ResolveTitleId(WikiLinkTarget target, Dictionary<string, Guid>? journalTitles) =>
        target.Form == WikiLinkKeyForm.Title
        && journalTitles is not null
        && journalTitles.TryGetValue(target.Key, out var id)
            ? id
            : null;

    private sealed record ProjectIndex(IReadOnlyList<(Guid Id, string Name, DateTime CreatedAt)> Rows)
    {
        public Guid? Resolve(WikiLinkTarget target)
        {
            if (target.Form == WikiLinkKeyForm.Guid)
            {
                return target.Id is { } projectId && Rows.Any(row => row.Id == projectId) ? projectId : null;
            }

            return ProjectNameMatch.PickStrict(Rows, target.Key);
        }
    }
}
