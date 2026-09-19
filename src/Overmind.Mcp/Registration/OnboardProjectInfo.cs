namespace Overmind.Mcp.Registration;

public sealed record OnboardProjectInfo
{
    public required string ProjectId { get; init; }
    public required string ProjectName { get; init; }
}
