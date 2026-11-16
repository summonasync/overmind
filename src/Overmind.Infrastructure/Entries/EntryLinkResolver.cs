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
        Guid? journalProjectId = null;

        foreach (var target in targets)
        {
            if (target.Form == WikiLinkKeyForm.Title && journalProjectId is null)
            {
                journalProjectId = await _journalProjects.EnsureIdAsync(ct);
            }

            var targetId = await ResolveTargetIdAsync(target, journalProjectId, ct);
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

    private async Task<Guid?> ResolveTargetIdAsync(
        WikiLinkTarget target, Guid? journalProjectId, CancellationToken ct) =>
        (target.Kind, target.Form) switch
        {
            (EntryLinkKind.Feature, WikiLinkKeyForm.Number) => await ResolveFeatureNumberAsync(target.Key, ct),
            (EntryLinkKind.Feature, WikiLinkKeyForm.Slug) => await ResolveFeatureSlugAsync(target.Key, ct),
            (EntryLinkKind.Project, WikiLinkKeyForm.Guid) => await ResolveProjectIdAsync(target.Id, ct),
            (EntryLinkKind.Project, WikiLinkKeyForm.Name) => await ResolveProjectNameAsync(target.Key, ct),
            (EntryLinkKind.Entry, WikiLinkKeyForm.Title) =>
                await ResolveJournalTitleAsync(target.Key, journalProjectId, ct),
            _ => null
        };

    private async Task<Guid?> ResolveFeatureNumberAsync(string key, CancellationToken ct)
    {
        if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

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

    private async Task<Guid?> ResolveFeatureSlugAsync(string key, CancellationToken ct)
    {
        var slug = key;
        var matches = await _db.PlanningFeatures
            .Where(f => f.Slug == slug)
            .Select(f => f.Id)
            .ToListAsync(ct);

        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Guid?> ResolveProjectIdAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } projectId)
        {
            return null;
        }

        return await _db.Projects.AnyAsync(p => p.Id == projectId, ct) ? projectId : null;
    }

    private async Task<Guid?> ResolveProjectNameAsync(string key, CancellationToken ct)
    {
        var lowered = key.ToLowerInvariant();
        var matches = await _db.Projects
            .Where(p => p.Name.ToLower() == lowered)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Guid?> ResolveJournalTitleAsync(string key, Guid? journalProjectId, CancellationToken ct)
    {
        if (journalProjectId is not { } projectId)
        {
            return null;
        }

        var lowered = key.ToLowerInvariant();
        return await _db.ContextEntries
            .Where(e => e.ProjectId == projectId && e.Type == EntryType.Journal && e.Title.ToLower() == lowered)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
    }
}
