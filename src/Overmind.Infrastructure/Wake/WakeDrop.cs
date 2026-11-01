namespace Overmind.Infrastructure.Wake;

public sealed record WakeDrop(
    string Alias,
    DateTime UpdatedAt,
    string Cursor,
    IReadOnlyList<AgentWakeSignal> Signals)
{
    public static WakeDrop For(string alias, DateTime updatedAt, AgentWakePage page) => new(
        alias,
        updatedAt,
        page.CursorRefId is { } id ? $"{page.Cursor:O}|{id}" : $"{page.Cursor:O}",
        page.Signals);
}
