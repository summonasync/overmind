using System.Globalization;
using System.Text.RegularExpressions;

namespace Overmind.Infrastructure.Planning;

public static partial class FeatureNumberPrefix
{
    public static bool TryParse(string slug, out int number)
    {
        var match = Pattern().Match(slug);
        if (match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            number = parsed;
            return true;
        }

        number = 0;
        return false;
    }

    [GeneratedRegex(@"^(\d+)-", RegexOptions.None, 200)]
    private static partial Regex Pattern();
}
