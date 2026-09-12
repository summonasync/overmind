using System.Security.Cryptography;
using System.Text;

namespace Overmind.Infrastructure.Materialization;

internal static class ContentHash
{
    internal static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string Normalize(string content) =>
        content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    public static string Sha256(string content)
    {
        var normalized = Normalize(content);
        return Convert.ToHexString(SHA256.HashData(Utf8NoBom.GetBytes(normalized)));
    }
}
