using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record OnboardRequest(string Cwd, Guid? ProjectId);

public sealed record OnboardBoardSummary(
    IReadOnlyDictionary<PlanningFeatureStatus, int> CountsByStatus,
    string? CurrentSlug);

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

public sealed record OnboardResponse(
    Guid ProjectId,
    string ProjectName,
    bool Created,
    OnboardBoardSummary Board,
    OnboardMaterializationBlock Materialization,
    OnboardValidationBlock Validation,
    OnboardSemanticBlock Semantic,
    string Quickstart);
