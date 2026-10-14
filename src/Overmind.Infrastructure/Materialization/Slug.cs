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
            if (CyrillicTransliteration.TryMap(c, out var latin))
            {
                AppendToken(builder, latin, ref dashPending);
            }
            else if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                AppendToken(builder, [c], ref dashPending);
            }
            else
            {
                dashPending = builder.Length > 0;
            }
        }

        return builder.Length == 0 ? fallback : builder.ToString();
    }

    private static void AppendToken(StringBuilder builder, ReadOnlySpan<char> token, ref bool dashPending)
    {
        if (token.IsEmpty)
        {
            return;
        }

        if (dashPending && builder.Length > 0)
        {
            builder.Append('-');
        }

        dashPending = false;
        builder.Append(token);
    }
}
