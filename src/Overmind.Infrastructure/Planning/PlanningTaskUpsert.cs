using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningTaskUpsert(
    string Body,
    int? Order = null,
    PlanningReviewLevel? Review = null,
    string? LogNote = null,
    string? Brief = null,
    IReadOnlyList<int>? BlockedBy = null,
    bool? Independent = null);
