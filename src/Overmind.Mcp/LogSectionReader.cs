namespace Overmind.Mcp;

internal static class LogSectionReader
{
    internal static string? LastEntryText(string body)
    {
        var lines = body.Split('\n');
        var close = LastIndexOfToken(lines, "</log>");
        if (close < 0)
        {
            return null;
        }

        var open = LastIndexOfToken(lines, close - 1, "<log>");
        if (open < 0)
        {
            return null;
        }

        for (var i = close - 1; i > open; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                return line[2..].TrimEnd();
            }
        }

        return null;
    }

    private static int LastIndexOfToken(string[] lines, string token) =>
        LastIndexOfToken(lines, lines.Length - 1, token);

    private static int LastIndexOfToken(string[] lines, int from, string token)
    {
        for (var i = from; i >= 0; i--)
        {
            if (lines[i].Trim() == token)
            {
                return i;
            }
        }

        return -1;
    }
}
