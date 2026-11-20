using System.Security.Cryptography;
using System.Text;

namespace Overmind.Infrastructure.Kits;

// Managed marker blocks for prose kits (feature 251 R6): `===OVERMIND <KIT> KIT
// START===` … `===OVERMIND <KIT> KIT END===`. Every operation is byte-stable
// outside the block; a kit only ever touches its own markers, so multiple kits
// coexist in one entry file.
public static class MarkerBlockWriter
{
    public static string KitToken(string kitName) =>
        new string(kitName.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    public static (string Start, string End) Markers(string kitName)
    {
        var token = KitToken(kitName);
        return ($"===OVERMIND {token} KIT START===", $"===OVERMIND {token} KIT END===");
    }

    public static string BuildBlock(string kitName, string content)
    {
        var (start, end) = Markers(kitName);
        var body = content.TrimEnd('\n');
        return $"{start}\n{body}\n{end}\n";
    }

    public static string ContentSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static (string Text, bool Changed, string ContentSha256) Upsert(
        string entryText, string kitName, string content)
    {
        var (start, end) = Markers(kitName);
        var block = BuildBlock(kitName, content);
        var startIndex = entryText.IndexOf(start, StringComparison.Ordinal);
        if (startIndex >= 0)
        {
            var endIndex = entryText.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                throw new InvalidOperationException($"Entry file has '{start}' without '{end}' — refusing to repair managed markers automatically.");
            }

            var existing = entryText[startIndex..(endIndex + end.Length)];
            var replacement = block.TrimEnd('\n');
            var newText = entryText[..startIndex] + replacement + entryText[(endIndex + end.Length)..];
            var unchanged = string.Equals(existing, replacement, StringComparison.Ordinal);
            return (newText, !unchanged, ContentSha256(replacement));
        }

        var withBlock = entryText.Length == 0
            ? block
            : (entryText.EndsWith('\n') ? entryText : entryText + "\n") + "\n" + block;
        return (withBlock, true, ContentSha256(block.TrimEnd('\n')));
    }

    public static (string Text, bool Removed) Cut(string entryText, string kitName)
    {
        var (start, end) = Markers(kitName);
        var startIndex = entryText.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return (entryText, false);
        }

        var endIndex = entryText.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return (entryText, false);
        }

        var cut = entryText.Remove(startIndex, endIndex + end.Length - startIndex);
        if (startIndex < cut.Length && startIndex > 0 && cut[startIndex - 1] == '\n')
        {
            var rest = cut[startIndex..];
            if (rest.StartsWith('\n'))
            {
                cut = cut.Remove(startIndex, 1);
            }
        }

        return (cut, true);
    }
}
