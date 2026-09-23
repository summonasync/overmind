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
    DateTime? LastRun,
    string? Error,
    DateTime? FailedAt);

public sealed record OnboardValidationBlock(bool HumanGate);

public sealed record OnboardResponse(
    Guid ProjectId,
    string ProjectName,
    bool Created,
    OnboardBoardSummary Board,
    OnboardMaterializationBlock Materialization,
    OnboardValidationBlock Validation,
    string Quickstart);
