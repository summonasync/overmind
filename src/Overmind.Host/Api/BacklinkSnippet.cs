using Overmind.Core;

namespace Overmind.Host.Api;

internal static class BacklinkSnippet
{
    private const int Window = 180;
    private const int Lead = 60;

    public static string From(string body, string targetTitle)
    {
        var written = WrittenTitle(body, targetTitle) ?? targetTitle;
        var at = body.IndexOf($"[[{written}]]", StringComparison.Ordinal);
        if (at < 0)
        {
            at = body.IndexOf(written, StringComparison.OrdinalIgnoreCase);
        }

        if (at < 0)
        {
            at = 0;
        }

        var start = Math.Max(0, at - Lead);
        var end = Math.Min(body.Length, start + Window);
        var text = Flatten(body[start..end]);

        return (start > 0 ? "..." : string.Empty) + text + (end < body.Length ? "..." : string.Empty);
    }

    // The edge was created by a bare-title marker, so the marker text is one of the parser's
    // entry targets — the title may have changed since the recompute, hence the null fallthrough.
    private static string? WrittenTitle(string body, string targetTitle) =>
        WikiLinkParser.Parse(body)
            .Where(target => target.Kind == EntryLinkKind.Entry)
            .Select(target => target.Key)
            .FirstOrDefault(key => key.Equals(targetTitle, StringComparison.OrdinalIgnoreCase));

    private static string Flatten(string text) =>
        string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
