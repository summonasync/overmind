namespace Overmind.Mcp.Registration;

public static class OnboardManagedBlock
{
    public const string StartMarker = "<!-- overmind:begin -->";
    public const string EndMarker = "<!-- overmind:end -->";

    public static string Merge(string? existingContent, string block)
    {
        if (existingContent is null)
        {
            return $"{StartMarker}\n{block}\n{EndMarker}\n";
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
}
