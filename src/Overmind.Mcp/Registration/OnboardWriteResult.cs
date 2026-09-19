namespace Overmind.Mcp.Registration;

public sealed record OnboardWriteResult
{
    public required OnboardWriteOutcome Outcome { get; init; }
    public required string FilePath { get; init; }
    public required IReadOnlyList<AgentFileConflict> Conflicts { get; init; }
}
