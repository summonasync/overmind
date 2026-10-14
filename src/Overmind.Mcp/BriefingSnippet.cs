using System.Globalization;

namespace Overmind.Mcp;

internal static class BriefingSnippet
{
    public static string Clip(string content, int cap)
    {
        var flattened = string.Join(' ', content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0));

        var info = new StringInfo(flattened);
        return info.LengthInTextElements <= cap
            ? flattened
            : info.SubstringByTextElements(0, cap) + "…";
    }
}
