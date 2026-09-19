namespace Overmind.Infrastructure.Planning;

internal static class BacklogNaming
{
    public static (string Feature, string Slug) SplitSlug(string folderSlug)
    {
        var dash = folderSlug.IndexOf('-');
        if (dash > 0 && dash < folderSlug.Length - 1 && folderSlug[..dash].All(char.IsAsciiDigit))
        {
            return (folderSlug[..dash], folderSlug[(dash + 1)..]);
        }

        return (folderSlug, folderSlug);
    }

    public static string TaskFileName(int order, string taskSlug) => $"{order:D2}-{taskSlug}.md";

    public static string? StripTitlePrefix(string? heading, string kind, string number)
    {
        if (heading is null)
        {
            return null;
        }

        var emDashPrefix = $"{kind} {number} — ";
        var colonPrefix = $"{kind} {number}: ";
        var title = heading.Trim();
        while (true)
        {
            if (title.StartsWith(emDashPrefix, StringComparison.Ordinal))
            {
                title = title[emDashPrefix.Length..].Trim();
            }
            else if (title.StartsWith(colonPrefix, StringComparison.Ordinal))
            {
                title = title[colonPrefix.Length..].Trim();
            }
            else
            {
                return title;
            }
        }
    }

    public static bool TryParseTaskOrder(string fileName, out int order, out char suffix)
    {
        order = 0;
        suffix = '\0';
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dash = fileName.IndexOf('-');
        if (dash <= 0)
        {
            return false;
        }

        var prefix = fileName[..dash];
        var digits = prefix;
        if (char.IsAsciiLetter(prefix[^1]))
        {
            suffix = prefix[^1];
            digits = prefix[..^1];
        }

        return digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, out order);
    }
}
