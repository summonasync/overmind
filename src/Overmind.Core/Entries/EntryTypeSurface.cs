namespace Overmind.Core;

public static class EntryTypeSurface
{
    // The one declaration of what the agent door writes. EntryTypeSurfaceTests (Core.Tests) asserts that this set
    // plus the two gated types cover the enum exactly, and EntryTypeSurfaceContractTests (Mcp.Tests) pins the
    // adapter's own lists to it — so a new EntryType member forces a decision instead of growing a prose copy.
    public static readonly EntryType[] AgentWritable =
    [
        EntryType.Note,
        EntryType.Decision,
        EntryType.Gate,
        EntryType.Guard,
        EntryType.Idea,
        EntryType.Audit,
        EntryType.Tombstone
    ];

    public static string AgentWritableNames { get; } =
        string.Join(", ", [.. AgentWritable.Select(Normalize)]);

    public static string[] AllNames { get; } = [.. Enum.GetValues<EntryType>().Select(Normalize)];

    public static string Normalize(EntryType type) => type.ToString().ToLowerInvariant();
}
