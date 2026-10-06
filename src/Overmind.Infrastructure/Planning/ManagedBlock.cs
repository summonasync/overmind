namespace Overmind.Infrastructure.Planning;

internal static class ManagedBlock
{
    public const string StartMarker = "<!-- overmind:planning:start -->";
    public const string EndMarker = "<!-- overmind:planning:end -->";

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
}
