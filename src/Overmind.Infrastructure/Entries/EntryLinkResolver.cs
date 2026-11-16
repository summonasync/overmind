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

    public async Task<IReadOnlyList<ResolvedLink>> ResolveAsync(
        Guid sourceEntryId,
        IReadOnlyList<WikiLinkTarget> targets,
        CancellationToken ct = default)
    {
        var resolved = new List<ResolvedLink>();
        var seen = new HashSet<(EntryLinkKind, Guid)>();
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
                journalTitles = await LoadJournalTitlesAsync(ct);
            }

            Guid? targetId = target.Kind switch
            {
                EntryLinkKind.Feature => await ResolveFeatureAsync(target, ct),
                EntryLinkKind.Project => projects!.Resolve(target),
                _ => ResolveTitleId(target, journalTitles)
            };

            if (targetId is not { } resolvedId || resolvedId == sourceEntryId)
            {
                continue;
            }

            if (seen.Add((target.Kind, resolvedId)))
            {
                resolved.Add(new ResolvedLink(target.Kind, resolvedId));
            }
        }

        return resolved;
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

    private async Task<Dictionary<string, Guid>> LoadJournalTitlesAsync(CancellationToken ct)
    {
        var projectId = await _journalProjects.EnsureIdAsync(ct);

        var rows = await _db.ContextEntries
            .Where(e => e.ProjectId == projectId && e.Type == EntryType.Journal)
            .OrderBy(e => e.UpdatedAt)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.Title })
            .ToListAsync(ct);

        var byTitle = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
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

            return ProjectNameMatch.Pick(Rows, target.Key);
        }
    }
}
