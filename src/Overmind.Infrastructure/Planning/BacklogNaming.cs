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
}
