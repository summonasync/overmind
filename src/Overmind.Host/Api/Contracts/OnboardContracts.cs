using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record OnboardRequest(string Cwd, Guid? ProjectId, bool Create = true);

public sealed record OnboardBoardSummary(
    IReadOnlyDictionary<PlanningFeatureStatus, int> CountsByStatus);

public sealed record OnboardMaterializationBlock(
    string Strategy,
    string TargetDir,
    string Trigger,
    DateTime? LastSuccess,
    DateTime? LastAttempt,
    string? Error,
    DateTime? FailedAt,
    int? SkippedCount,
    IReadOnlyList<string>? Skipped,
    string? DemotedNotice = null);

public sealed record OnboardValidationBlock(bool HumanGate, string Guidance);

public sealed record OnboardSemanticBlock(bool Available, string? Reason);

// The pair is the caller's live session row when the request carried a session header the host
// could resolve; without one it falls back to the placeholder defaults and the adapter renders
// its own environment instead.
public sealed record OnboardAgentIdentityBlock(string Alias, string Vendor, string NamingRules);

// Claims whose holder session is gone: the pair the board still reads, how the holder died and when
// it was last seen, and the slugs it holds. Liveness is "ended" or "stale"; UnseenSince is the end
// stamp for an ended holder and the last heartbeat for a stale one. SessionsTotal counts churned
// holders before the row cap, so the holders dropped from the list are still accounted for.
public sealed record OnboardClaimChurnBlock(
    IReadOnlyList<OnboardChurnedSession> Sessions,
    int SessionsTotal);

public sealed record OnboardChurnedSession(
    string Alias,
    string Vendor,
    string Liveness,
    DateTime UnseenSince,
    int DeathAgeMinutes,
    IReadOnlyList<string> Slugs,
    int SlugsTotal);

public sealed record OnboardAdapterBlock(string? AdapterVersion, string HostVersion, string? Warning);

public sealed record OnboardRescueBlock(string? PolicyText);

public sealed record OnboardResponse(
    Guid ProjectId,
    string ProjectName,
    bool Created,
    OnboardBoardSummary Board,
    OnboardMaterializationBlock Materialization,
    OnboardValidationBlock Validation,
    OnboardSemanticBlock Semantic,
    OnboardAgentIdentityBlock AgentIdentity,
    OnboardClaimChurnBlock? ClaimChurn,
    OnboardAdapterBlock Adapter,
    OnboardRescueBlock Rescue,
    string Quickstart);
