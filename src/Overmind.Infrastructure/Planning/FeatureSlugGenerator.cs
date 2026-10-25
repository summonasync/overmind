using System.Globalization;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

public static class FeatureSlugGenerator
{
    public const int MaxLength = 64;

    // Titles go through the mirror-filename transliteration (Materialization/CyrillicTransliteration,
    // GOST 7.79-2000 System B family): ё→yo, й→y, щ→shch, hard and soft signs dropped.
    public static string SlugPart(string title) => Slug.From(title, "feature");

    public static string Compose(IReadOnlyCollection<string> takenSlugs, string title)
    {
        var part = SlugPart(title);
        var taken = new HashSet<string>(takenSlugs, StringComparer.Ordinal);
        var numbers = new HashSet<int>();
        var max = 0;
        foreach (var takenSlug in takenSlugs)
        {
            if (FeatureNumberPrefix.TryParse(takenSlug, out var number))
            {
                numbers.Add(number);
                max = Math.Max(max, number);
            }
        }

        var candidate = max + 1;
        while (numbers.Contains(candidate) || taken.Contains(Build(candidate, part)))
        {
            candidate++;
        }

        return Build(candidate, part);
    }

    private static string Build(int number, string slugPart)
    {
        var prefix = number.ToString("000", CultureInfo.InvariantCulture) + "-";
        var budget = MaxLength - prefix.Length;
        var part = slugPart.Length > budget ? slugPart[..budget].TrimEnd('-') : slugPart;
        return prefix + part;
    }
}
