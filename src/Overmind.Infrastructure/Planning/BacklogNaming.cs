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

    public static bool TryParseTaskOrder(string fileName, out int order)
    {
        order = 0;
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dash = fileName.IndexOf('-');
        if (dash <= 0)
        {
            return false;
        }

        var digits = fileName[..dash];
        return digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, out order);
    }
}
