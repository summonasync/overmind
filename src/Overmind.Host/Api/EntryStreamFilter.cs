using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

// The two stream filters spec R5 promised the inbox, read off the query string of the existing list endpoint.
// An unusable value answers with a typed 400 rather than an empty page: a silently empty inbox would read as
// "nothing to triage", which is the one answer the owner must never get by accident.
internal static class EntryStreamFilter
{
    public static IResult? ValidateLink(string? requested, out bool? linked)
    {
        linked = null;
        var value = requested?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Equals("linked", StringComparison.OrdinalIgnoreCase))
        {
            linked = true;
            return null;
        }

        if (value.Equals("unlinked", StringComparison.OrdinalIgnoreCase))
        {
            linked = false;
            return null;
        }

        return HttpErrors.BadRequest(
            $"Unknown link filter '{requested}'. Valid values: linked, unlinked.",
            "UnknownLinkFilter");
    }

    public static async Task<(Guid? ProjectId, IResult? Failure)> ResolveProjectAsync(
        OvermindDbContext db,
        string? requested,
        CancellationToken ct)
    {
        var value = requested?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return (null, null);
        }

        if (Guid.TryParse(value, out var projectId))
        {
            return await db.Projects.AnyAsync(project => project.Id == projectId, ct)
                ? (projectId, null)
                : (null, HttpErrors.BadRequest(
                    $"Project filter '{value}' names no project id.",
                    "UnresolvedProjectFilter"));
        }

        // Indexed fold (IX_Projects_NameNormalized), not a full-table sweep: the unique index
        // caps candidates at one row, so the strict pick below keeps its shape for free. The
        // Latin-only store invariant (probed 2026-11-21, recorded on the migration) is what
        // makes the C# fold queryable — legacy non-ASCII rows would need the in-memory filter.
        var normalized = Project.NormalizeName(value);
        var candidates = (await db.Projects
                .Where(project => project.NameNormalized == normalized)
                .Select(project => new { project.Id, project.Name, project.CreatedAt })
                .ToArrayAsync(ct))
            .Select(row => (row.Id, row.Name, row.CreatedAt))
            .ToArray();

        if (candidates.Length == 0)
        {
            return (null, HttpErrors.BadRequest(
                $"Project filter '{value}' names no project.",
                "UnresolvedProjectFilter"));
        }

        // Marker grammar, not the reserved-container grammar: a case-variant tie is ambiguity the owner has to
        // see, never a silent pick of whichever project was created first.
        var match = ProjectNameMatch.PickStrict(candidates, value);

        return match is { } resolved
            ? (resolved, null)
            : (null, HttpErrors.BadRequest(
                $"Project filter '{value}' is ambiguous between " +
                $"{string.Join(", ", candidates.Select(candidate => candidate.Name))}.",
                "AmbiguousProjectFilter"));
    }

    public static IQueryable<ContextEntry> Apply(
        IQueryable<ContextEntry> query,
        EntryLinkTriage triage,
        bool? linked,
        Guid? projectId)
    {
        if (linked is { } wantLinked)
        {
            query = wantLinked ? triage.WhereLinked(query) : triage.WhereUnlinked(query);
        }

        if (projectId is { } target)
        {
            query = triage.WhereLinkedToProject(query, target);
        }

        return query;
    }
}
