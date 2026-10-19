namespace Overmind.Mcp.Registration;

public sealed record InstallOptions
{
    public bool All { get; init; }
    public IReadOnlyList<string> Agents { get; init; } = [];
    public bool Project { get; init; }
    public bool Onboard { get; init; }
    public bool Remove { get; init; }
    public bool WithGrimoire { get; init; }
    public bool RemoveGrimoire { get; init; }
    public bool DryRun { get; init; }
}
