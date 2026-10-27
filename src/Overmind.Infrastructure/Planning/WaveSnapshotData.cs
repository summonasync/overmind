using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record WaveSnapshotData(
    Guid ProjectId,
    DateTime GeneratedAt,
    IReadOnlyList<string> CoordinationTags,
    IReadOnlyList<PlanningFeature> QueueFeatures,
    IReadOnlyList<PlanningFeature> InFlightFeatures,
    IReadOnlyDictionary<Guid, AgentSessionLiveness> ClaimLiveness,
    IReadOnlyList<ContextEntry> CoordinationEntries,
    IReadOnlyDictionary<Guid, int> OpenDirectiveCounts,
    IReadOnlyDictionary<string, string?> LaneBoundAliases);
