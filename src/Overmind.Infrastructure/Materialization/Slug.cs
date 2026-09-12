using System.Text;

namespace Overmind.Infrastructure.Materialization;

internal static class Slug
{
    public static string From(string value, string fallback)
    {
        var builder = new StringBuilder(value.Length);
        var dashPending = false;

        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
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

        return builder.Length == 0 ? fallback : builder.ToString();
    }
}
