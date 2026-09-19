namespace Overmind.Mcp.Registration;

public sealed record AgentDefinition
{
    public required string Name { get; init; }
    public required AgentConfigFormat ConfigFormat { get; init; }
    public required string DefaultUserConfigTemplate { get; init; }
    public string? UserConfigEnvironmentVariable { get; init; }
    public string? UserConfigEnvironmentFileName { get; init; }
    public required string ProjectRelativePath { get; init; }
    public required IReadOnlyList<string> ConfigDirCandidates { get; init; }
    public required IReadOnlyList<string> BinaryNames { get; init; }
    public IReadOnlyList<string> AgentFileCandidates { get; init; } = [];
}
