using System.Text.Json;
using System.Text.Json.Nodes;

namespace Overmind.Mcp;

public sealed record RescueCandidateRow(
    string Slug,
    string ClaimState,
    string? PinnedBranch,
    string? PinnedWorktreePath,
    JsonObject Source)
{
    public static RescueCandidateRow From(JsonElement element)
    {
        var clone = (JsonObject)JsonNode.Parse(element.GetRawText())!;

        return new RescueCandidateRow(
            Text(clone, "slug"),
            Text(clone, "claimState"),
            NullableText(clone, "pinnedBranch"),
            NullableText(clone, "pinnedWorktreePath"),
            clone);
    }

    private static string Text(JsonObject source, string property) =>
        NullableText(source, property) ?? string.Empty;

    private static string? NullableText(JsonObject source, string property) =>
        source[property] is JsonValue value && value.TryGetValue(out string? text)
            ? text
            : null;
}
