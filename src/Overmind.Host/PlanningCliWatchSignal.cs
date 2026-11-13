namespace Overmind.Host;

internal sealed record PlanningCliWatchSignal(
    string Kind,
    Guid RefId,
    string? RoomId,
    string From,
    DateTimeOffset CreatedAt,
    string Excerpt);
