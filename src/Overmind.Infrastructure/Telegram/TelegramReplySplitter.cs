using System.Text;

namespace Overmind.Infrastructure.Telegram;

public static class TelegramReplySplitter
{
    public const int MaxMessageLength = 4096;

    public static IReadOnlyList<string> Split(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        if (trimmed.Length <= MaxMessageLength)
        {
            return [trimmed];
        }

        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var paragraph in EnumerateParagraphs(trimmed))
        {
            if (paragraph.Length > MaxMessageLength)
            {
                Flush(current, chunks);
                HardSplit(paragraph, chunks);
                continue;
            }

            var separator = current.Length == 0 ? string.Empty : "\n\n";
            if (current.Length + separator.Length + paragraph.Length > MaxMessageLength)
            {
                Flush(current, chunks);
                separator = string.Empty;
            }

            current.Append(separator).Append(paragraph);
        }

        Flush(current, chunks);
        return chunks;
    }

    private static IEnumerable<string> EnumerateParagraphs(string text)
    {
        return text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void HardSplit(string paragraph, List<string> chunks)
    {
        for (var offset = 0; offset < paragraph.Length; offset += MaxMessageLength)
        {
            var length = Math.Min(MaxMessageLength, paragraph.Length - offset);
            chunks.Add(paragraph.Substring(offset, length));
        }
    }

    private static void Flush(StringBuilder current, List<string> chunks)
    {
        if (current.Length == 0)
        {
            return;
        }

        chunks.Add(current.ToString());
        current.Clear();
    }
}
