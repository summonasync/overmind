using System.Text;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

internal static class BacklogNaming
{
    private const int SlugByteBudget = 48;

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

    // Mirror names must survive titles the filesystem cannot: the slug keeps only ASCII word
    // characters within a fixed byte budget, and any information the slug dropped (non-ASCII
    // characters, truncation) is marked with a short hash of the full source so distinct titles
    // never converge on one name. No transliteration — a hash suffix beats an unreadable guess.
    public static string MirrorSlug(string title, string fallback)
    {
        var source = title.Trim();
        var builder = new StringBuilder(source.Length);
        var dashPending = false;
        var lossless = true;

        foreach (var raw in source)
        {
            lossless &= char.IsAscii(raw);
            var c = char.ToLowerInvariant(raw);
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            {
                if (dashPending && builder.Length > 0)
                {
                    builder.Append('-');
                }

                dashPending = false;
                builder.Append(c);
            }
            else
            {
                dashPending = builder.Length > 0;
            }
        }

        if (builder.Length > SlugByteBudget)
        {
            lossless = false;
            builder.Length = SlugByteBudget;
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == '-')
        {
            builder.Length--;
        }

        var slug = builder.Length == 0 ? fallback : builder.ToString();
        return lossless ? slug : $"{slug}-{ContentHash.Sha256(source)[..8].ToLowerInvariant()}";
    }

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

    // A tracked name survives the pass only while the current scheme could have produced it:
    // bounded ASCII slug, and the loss marker matching what the fresh slug carries. Names the
    // old transliteration pipeline left behind (unbounded, non-ASCII, unmarked while the title
    // demands a marker) fail the check and converge onto the fresh name in the same pass.
    public static bool KeepsTaskFileName(string? trackedFileName, int order, string freshSlug)
    {
        if (trackedFileName is null ||
            !TryParseTaskOrder(trackedFileName, out var trackedOrder, out _) ||
            trackedOrder != order)
        {
            return false;
        }

        return KeepsSlugPart(SlugPart(trackedFileName), freshSlug);
    }

    public static bool KeepsEntryFileName(string? trackedFileName, string freshSlug, string id8)
    {
        if (trackedFileName is null)
        {
            return false;
        }

        var stem = Stem(trackedFileName);
        var anchor = $"-{id8}";
        if (!stem.EndsWith(anchor, StringComparison.Ordinal))
        {
            return false;
        }

        return KeepsSlugPart(stem[..^anchor.Length], freshSlug);
    }

    private static bool KeepsSlugPart(string trackedSlug, string freshSlug)
    {
        if (trackedSlug.Length == 0 || !trackedSlug.All(char.IsAscii))
        {
            return false;
        }

        var trackedMarked = TryStripLossMarker(trackedSlug, out var trackedBase);
        if (trackedBase.Length > SlugByteBudget)
        {
            return false;
        }

        var freshMarked = TryStripLossMarker(freshSlug, out _);
        return trackedMarked == freshMarked && (!trackedMarked || trackedSlug == freshSlug);
    }

    private static bool TryStripLossMarker(string slug, out string baseSlug)
    {
        baseSlug = slug;
        if (slug.Length < 9 || slug[^9] != '-')
        {
            return false;
        }

        var marker = slug[^8..];
        if (!marker.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        baseSlug = slug[..^9];
        return baseSlug.Length > 0;
    }

    private static string SlugPart(string fileName) =>
        Stem(fileName)[(fileName.IndexOf('-') + 1)..];

    private static string Stem(string fileName) =>
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^3]
            : fileName;
}
