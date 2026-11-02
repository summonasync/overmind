using System.Text;

namespace Overmind.Infrastructure.Grimoire;

internal static class GrimoireCompactionPrompts
{
    public const int MaxEntriesPerPrompt = 24;
    public const int MaxEntryChars = 400;

    public static string Distill(IReadOnlyList<GrimoireMemoryRecord> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("These journal memory entries were written around the same topic:");
        builder.AppendLine();
        foreach (var entry in entries.Take(MaxEntriesPerPrompt))
        {
            builder.AppendLine($"- {Truncate(entry.Content)}");
        }

        builder.AppendLine();
        builder.Append(
            "Distill the standing information they repeat into ONE durable fact. Self-contained, English, " +
            "keep concrete dates and names, at most 3 sentences. Reply with the fact text only — no preamble, no quotes.");
        return builder.ToString();
    }

    private static string Truncate(string content) =>
        content.Length <= MaxEntryChars ? content.ReplaceLineEndings(" ") : content[..MaxEntryChars].ReplaceLineEndings(" ") + "…";
}
