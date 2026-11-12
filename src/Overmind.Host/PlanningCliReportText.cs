using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCliReportText
{
    private const string Unattributed = "unattributed";

    internal static string Render(JsonElement entries, DateOnly day)
    {
        var rows = entries.ValueKind == JsonValueKind.Array ? entries.EnumerateArray().ToArray() : [];
        if (rows.Length == 0)
        {
            return $"No entries on {day:yyyy-MM-dd}\n";
        }

        var builder = new StringBuilder();
        builder.Append($"# Report {day:yyyy-MM-dd}\n\n");
        foreach (var group in rows
            .Select(row => (Row: row, Author: Author(row)))
            .GroupBy(pair => pair.Author, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.Append($"## {group.Key}\n\n");
            foreach (var (row, _) in group.OrderBy(pair => CreatedAt(pair.Row)))
            {
                builder.Append($"### {PlanningCliJson.Text(row, "title")}\n\n");
                var content = PlanningCliJson.Text(row, "content");
                builder.Append(content);
                if (!content.EndsWith('\n'))
                {
                    builder.Append('\n');
                }

                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string Author(JsonElement row)
    {
        var alias = PlanningCliJson.Text(row, "authorAlias");
        return alias.Length == 0 ? Unattributed : alias;
    }

    private static DateTime CreatedAt(JsonElement row) =>
        DateTime.TryParse(
            PlanningCliJson.Text(row, "createdAt"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var stamp)
            ? stamp
            : DateTime.MaxValue;
}
