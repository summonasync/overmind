using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record OnboardRequest(string Cwd, Guid? ProjectId);

public sealed record OnboardBoardSummary(
    IReadOnlyDictionary<PlanningFeatureStatus, int> CountsByStatus,
    string? CurrentSlug);

public sealed record OnboardResponse(
    Guid ProjectId,
    string ProjectName,
    bool Created,
    OnboardBoardSummary Board,
    string Quickstart);
