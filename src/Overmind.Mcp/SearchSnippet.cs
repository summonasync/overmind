namespace Overmind.Mcp;

internal static class SearchSnippet
{
    private const int WindowSize = 5;

    public static string Window(string text, string query)
    {
        var lines = text
            .Replace("<mark>", string.Empty)
            .Replace("</mark>", string.Empty)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

        var anchor = FindAnchor(lines, query);
        var start = Math.Clamp(anchor - WindowSize / 2, 0, Math.Max(0, lines.Length - WindowSize));
        var end = Math.Min(start + WindowSize, lines.Length);

        var window = string.Join('\n', lines[start..end]);
        if (start > 0)
        {
            window = "...\n" + window;
        }

        if (end < lines.Length)
        {
            window += "\n...";
        }

        return window;
    }

    private static int FindAnchor(string[] lines, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length > 0)
        {
            var whole = IndexOfLineContaining(lines, trimmed);
            if (whole >= 0)
            {
                return whole;
            }

            foreach (var term in trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var byTerm = IndexOfLineContaining(lines, term);
                if (byTerm >= 0)
                {
                    return byTerm;
                }
            }
        }

        return 0;
    }

    private static int IndexOfLineContaining(string[] lines, string needle) =>
        Array.FindIndex(lines, line => line.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
