using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

internal static class ManagedBlock
{
    public const string StartMarker = "<!-- overmind:planning:start -->";
    public const string EndMarker = "<!-- overmind:planning:end -->";

    public static string Render(IEnumerable<PlanningFeature> features) =>
        string.Join(
            "\n",
            features.Select(f =>
                $"- `{StatusMarker(f.Status)}` {f.Slug} → `BACKLOG/{f.Slug}/` — {f.Title}"));

    public static string Merge(string? existingContent, string block)
    {
        if (existingContent is null)
        {
            return $"# BACKLOG\n\n{StartMarker}\n{block}\n{EndMarker}\n";
        }

        var start = existingContent.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = existingContent.IndexOf(EndMarker, start + StartMarker.Length, StringComparison.Ordinal);
            if (end >= 0)
            {
                return existingContent[..start] +
                       StartMarker + "\n" + block + "\n" + EndMarker +
                       existingContent[(end + EndMarker.Length)..];
            }
        }

        var separator = existingContent.EndsWith("\n\n", StringComparison.Ordinal)
            ? string.Empty
            : existingContent.EndsWith('\n')
                ? "\n"
                : "\n\n";

        return $"{existingContent}{separator}{StartMarker}\n{block}\n{EndMarker}\n";
    }

    public static bool TryExtract(string content, out string block)
    {
        block = string.Empty;

        var start = content.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var innerStart = start + StartMarker.Length;
        var end = content.IndexOf(EndMarker, innerStart, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var inner = content[innerStart..end];
        if (inner.StartsWith('\n'))
        {
            inner = inner[1..];
        }

        if (inner.EndsWith('\n'))
        {
            inner = inner[..^1];
        }

        block = inner;
        return true;
    }

    public static string Remove(string content)
    {
        var start = content.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return content;
        }

        var end = content.IndexOf(EndMarker, start + StartMarker.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            return content;
        }

        var prefix = content[..start];
        if (prefix.EndsWith("\n\n", StringComparison.Ordinal))
        {
            prefix = prefix[..^1];
        }

        var suffix = content[(end + EndMarker.Length)..];
        if (suffix.StartsWith('\n'))
        {
            suffix = suffix[1..];
        }

        return prefix + suffix;
    }

    private static string StatusMarker(PlanningFeatureStatus status) => status switch
    {
        PlanningFeatureStatus.Draft or PlanningFeatureStatus.Ready => "[ ]",
        PlanningFeatureStatus.InProgress or PlanningFeatureStatus.Review => "[~]",
        PlanningFeatureStatus.Done => "[x]",
        _ => "[ ]",
    };
}
