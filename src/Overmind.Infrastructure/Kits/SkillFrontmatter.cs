namespace Overmind.Infrastructure.Kits;

internal static class SkillFrontmatter
{
    public static bool TryParse(
        string content, out IReadOnlyDictionary<string, string> fields, out string? error)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        fields = parsed;
        error = null;

        var lines = content.Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd('\r') != "---")
        {
            error = "Frontmatter must open with '---' on line 1.";
            return false;
        }

        var closingLine = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == "---")
            {
                closingLine = i;
                break;
            }
        }

        if (closingLine < 0)
        {
            error = "Frontmatter closing '---' delimiter is missing.";
            return false;
        }

        for (var i = 1; i < closingLine; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('\t'))
            {
                error = "Frontmatter must be indented with spaces, not tabs.";
                return false;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            parsed[key] = line[(colon + 1)..].Trim().Trim('"', '\'');
        }

        return true;
    }
}
