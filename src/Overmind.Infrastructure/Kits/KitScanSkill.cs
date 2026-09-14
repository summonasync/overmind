using Overmind.Core;

namespace Overmind.Infrastructure.Kits;

public sealed class KitScanSkill
{
    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public AgentPlatform? Platform { get; init; }

    public int FileCount { get; init; }
}
