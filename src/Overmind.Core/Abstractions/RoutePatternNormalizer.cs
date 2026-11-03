using System.Text;

namespace Overmind.Core;

public static class RoutePatternNormalizer
{
    public static string Normalize(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var builder = new StringBuilder(pattern.Length);
        var pendingSpace = false;
        foreach (var ch in pattern)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                pendingSpace = builder.Length > 0;
            }
        }

        return builder.ToString();
    }
}
