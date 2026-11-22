using Overmind.Core;

namespace Overmind.Host.Api;

internal static class EntryQueryFilters
{
    public static IQueryable<ContextEntry> Apply(
        IQueryable<ContextEntry> query,
        DateTime? since,
        DateTime? until,
        string? author,
        DateTime? activitySince = null,
        DateTime? activityUntil = null)
    {
        var sinceUtc = ToUtc(since);
        var untilUtc = ToUtc(until);
        var activitySinceUtc = ToUtc(activitySince);
        var activityUntilUtc = ToUtc(activityUntil);
        var alias = Author(author)?.ToLowerInvariant();

        if (sinceUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= sinceUtc.Value);
        }

        if (untilUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt < untilUtc.Value);
        }

        if (activitySinceUtc.HasValue)
        {
            query = query.Where(e => e.UpdatedAt >= activitySinceUtc.Value);
        }

        if (activityUntilUtc.HasValue)
        {
            query = query.Where(e => e.UpdatedAt < activityUntilUtc.Value);
        }

        if (alias is not null)
        {
            query = query.Where(e => e.AuthorAlias!.ToLower() == alias);
        }

        return query;
    }

    public static bool Matches(
        ContextEntry entry,
        DateTime? since,
        DateTime? until,
        string? author,
        DateTime? activitySince = null,
        DateTime? activityUntil = null)
    {
        var sinceUtc = ToUtc(since);
        var untilUtc = ToUtc(until);
        var activitySinceUtc = ToUtc(activitySince);
        var activityUntilUtc = ToUtc(activityUntil);
        var alias = Author(author);

        return (!sinceUtc.HasValue || entry.CreatedAt >= sinceUtc.Value)
            && (!untilUtc.HasValue || entry.CreatedAt < untilUtc.Value)
            && (!activitySinceUtc.HasValue || entry.UpdatedAt >= activitySinceUtc.Value)
            && (!activityUntilUtc.HasValue || entry.UpdatedAt < activityUntilUtc.Value)
            && (alias is null
                || string.Equals(entry.AuthorAlias, alias, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime? ToUtc(DateTime? value) => value?.ToUniversalTime();

    private static string? Author(string? author)
    {
        var trimmed = author?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
