using System.Globalization;

namespace Overmind.Core;

public static class WikiLinkParser
{
    private const string OpenMarker = "[[";
    private const string CloseMarker = "]]";
    private const string FeaturePrefix = "feature:";
    private const string ProjectPrefix = "project:";

    public static IReadOnlyList<WikiLinkTarget> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var links = new List<WikiLinkTarget>();
        var seen = new HashSet<WikiLinkTarget>();
        var openFence = '\0';

        foreach (var line in body.Split('\n'))
        {
            var fence = FenceMarker(line);
            if (fence != '\0')
            {
                if (openFence == '\0')
                {
                    openFence = fence;
                }
                else if (openFence == fence)
                {
                    openFence = '\0';
                }

                continue;
            }

            if (openFence != '\0')
            {
                continue;
            }

            CollectLine(line, links, seen);
        }

        return links;
    }

    private static void CollectLine(string line, List<WikiLinkTarget> links, HashSet<WikiLinkTarget> seen)
    {
        var index = 0;

        while (index < line.Length)
        {
            if (line[index] == '`')
            {
                index = SkipInlineCode(line, index);
                continue;
            }

            if (line[index] == '[' &&
                index + 1 < line.Length &&
                line[index + 1] == '[')
            {
                var close = line.IndexOf(CloseMarker, index + OpenMarker.Length, StringComparison.Ordinal);
                if (close < 0)
                {
                    return;
                }

                if (ToTarget(line.AsSpan(index + OpenMarker.Length, close - index - OpenMarker.Length)) is { } target &&
                    seen.Add(target))
                {
                    links.Add(target);
                }

                index = close + CloseMarker.Length;
                continue;
            }

            index++;
        }
    }

    private static int SkipInlineCode(string line, int start)
    {
        var run = 0;
        while (start + run < line.Length && line[start + run] == '`')
        {
            run++;
        }

        var search = start + run;
        while (search < line.Length)
        {
            var next = line.IndexOf('`', search);
            if (next < 0)
            {
                break;
            }

            var length = 0;
            while (next + length < line.Length && line[next + length] == '`')
            {
                length++;
            }

            if (length == run)
            {
                return next + length;
            }

            search = next + length;
        }

        return start + run;
    }

    private static WikiLinkTarget? ToTarget(ReadOnlySpan<char> raw)
    {
        var target = raw.Trim();
        if (target.IsEmpty)
        {
            return null;
        }

        if (target.StartsWith(FeaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = target[FeaturePrefix.Length..].Trim();
            if (key.IsEmpty)
            {
                return null;
            }

            return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? new WikiLinkTarget(EntryLinkKind.Feature, WikiLinkKeyForm.Number, key.ToString())
                : new WikiLinkTarget(EntryLinkKind.Feature, WikiLinkKeyForm.Slug, key.ToString());
        }

        if (target.StartsWith(ProjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = target[ProjectPrefix.Length..].Trim();
            if (key.IsEmpty)
            {
                return null;
            }

            return Guid.TryParse(key, out var projectId)
                ? new WikiLinkTarget(EntryLinkKind.Project, WikiLinkKeyForm.Guid, projectId.ToString(), projectId)
                : new WikiLinkTarget(EntryLinkKind.Project, WikiLinkKeyForm.Name, key.ToString());
        }

        return new WikiLinkTarget(EntryLinkKind.Entry, WikiLinkKeyForm.Title, target.ToString());
    }

    private static char FenceMarker(string line)
    {
        var trimmed = line.TrimStart(' ', '\t');
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return '`';
        }

        return trimmed.StartsWith("~~~", StringComparison.Ordinal) ? '~' : '\0';
    }
}
