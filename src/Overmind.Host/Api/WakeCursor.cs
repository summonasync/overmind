using System.Globalization;

namespace Overmind.Host.Api;

public static class WakeCursor
{
    private const char RefSeparator = '|';

    public static string Encode(DateTime at, Guid? refId) =>
        refId is { } id ? $"{at:O}{RefSeparator}{id}" : $"{at:O}";

    public static bool TryDecode(string? value, out DateTime? at, out Guid? refId)
    {
        at = null;
        refId = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parts = value.Split(RefSeparator);
        if (parts.Length > 2
            || !DateTime.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return false;
        }

        at = parsed;
        if (parts.Length == 2)
        {
            if (!Guid.TryParse(parts[1], out var id))
            {
                return false;
            }

            refId = id;
        }

        return true;
    }
}
