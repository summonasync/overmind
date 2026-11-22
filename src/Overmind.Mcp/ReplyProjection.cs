using System.Text.Json;

namespace Overmind.Mcp;

// The compact row for overmind_replies (266/T3 row 8): the keys a polling agent actually reads —
// which notification this answers, who answered, what they said, when — plus the excerpted text so
// a gate can still be recognised. Dropped: projectId (the query already scoped it), readAt and
// hiddenAt (the projection marks nothing that changes consumption). Text over 200 chars keeps its
// head and the row says so; nothing else is cut, so the reply SET is identical to the raw payload.
internal static class ReplyProjection
{
    public const int TextExcerptChars = 200;

    public static JsonElement Compact(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return payload;
        }

        var rows = new List<Dictionary<string, object?>>();
        var truncated = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var text = Text(item, "text");
            var excerpt = text is { Length: > TextExcerptChars } ? text[..TextExcerptChars] : text;
            if (excerpt != text)
            {
                truncated++;
            }

            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = Text(item, "id"),
                ["kind"] = Text(item, "kind"),
                ["featureSlug"] = Text(item, "featureSlug"),
                ["tab"] = Text(item, "tab"),
                ["agent"] = Text(item, "agent"),
                ["createdAt"] = Value(item, "createdAt"),
                ["text"] = excerpt,
                ["replyText"] = Text(item, "replyText"),
                ["repliedAt"] = Value(item, "repliedAt"),
            });
        }

        var projected = new Dictionary<string, object?>
        {
            ["projection"] = "compact",
            ["textExcerptChars"] = TextExcerptChars,
            ["items"] = rows,
        };

        if (truncated > 0)
        {
            projected["textExcerpted"] = truncated;
            projected["fullRows"] = "pass full=true for the stored notification texts";
        }

        return JsonSerializer.SerializeToElement(projected, JSON);
    }

    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static object? Value(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.GetRawText(),
            }
            : null;
}
