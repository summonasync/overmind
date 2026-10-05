namespace Overmind.Mcp;

internal static class LogSectionReader
{
    internal static string? SectionContentText(string body)
    {
        var lines = body.Split('\n');
        var close = LastIndexOfToken(lines, "</log>");
        if (close >= 0)
        {
            var open = LastIndexOfToken(lines, close - 1, "<log>");
            if (open >= 0)
            {
                return string.Join('\n', lines[(open + 1)..close]).Trim();
            }
        }

        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            var closeAt = line.IndexOf("</log>", StringComparison.Ordinal);
            var openAt = closeAt >= 0 ? line.LastIndexOf("<log>", closeAt, StringComparison.Ordinal) : -1;
            if (openAt >= 0)
            {
                return line[(openAt + 5)..closeAt].Trim();
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
