namespace Overmind.Host.Api.Contracts;

public sealed record AgentWakeSignalResponse(
    string Kind,
    Guid RefId,
    string? RoomId,
    string From,
    DateTime CreatedAt,
    string Excerpt);

public sealed record AgentWakeResponse(AgentWakeSignalResponse[] Signals, string Cursor);
