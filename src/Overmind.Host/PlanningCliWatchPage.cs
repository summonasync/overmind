using System.Text.Json;

namespace Overmind.Host;

internal sealed record PlanningCliWatchPage(IReadOnlyList<PlanningCliWatchSignal> Signals, string Cursor)
{
    internal static PlanningCliWatchPage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new(
            [.. root.GetProperty("signals").EnumerateArray().Select(ReadSignal)],
            root.GetProperty("cursor").GetString() ?? string.Empty);
    }

    private static PlanningCliWatchSignal ReadSignal(JsonElement signal) => new(
        PlanningCliJson.Text(signal, "kind"),
        signal.GetProperty("refId").GetGuid(),
        Room(signal),
        PlanningCliJson.Text(signal, "from"),
        signal.GetProperty("createdAt").GetDateTimeOffset(),
        PlanningCliJson.Text(signal, "excerpt"));

    private static string? Room(JsonElement signal) =>
        signal.TryGetProperty("roomId", out var room) && room.ValueKind == JsonValueKind.String
            ? room.GetString()
            : null;
}
