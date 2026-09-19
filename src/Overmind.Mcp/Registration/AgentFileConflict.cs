namespace Overmind.Mcp.Registration;

public sealed record AgentFileConflict
{
    public required int LineNumber { get; init; }
    public required string Text { get; init; }
}
