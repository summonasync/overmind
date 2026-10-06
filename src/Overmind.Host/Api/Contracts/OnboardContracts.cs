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
    IReadOnlyList<string>? Skipped);

public sealed record OnboardValidationBlock(bool HumanGate);

public sealed record OnboardSemanticBlock(bool Available, string? Reason);

public sealed record OnboardAgentIdentityBlock(string Alias, string Vendor, string NamingRules);

public sealed record OnboardAdapterBlock(string? AdapterVersion, string HostVersion, string? Warning);

public sealed record OnboardResponse(
    Guid ProjectId,
    string ProjectName,
    bool Created,
    OnboardBoardSummary Board,
    OnboardMaterializationBlock Materialization,
    OnboardValidationBlock Validation,
    OnboardSemanticBlock Semantic,
    OnboardAgentIdentityBlock AgentIdentity,
    OnboardAdapterBlock Adapter,
    string Quickstart);
