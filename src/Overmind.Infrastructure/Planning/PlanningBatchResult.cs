namespace Overmind.Infrastructure.Planning;

public sealed record PlanningBatchResult(
    string FeatureSlug,
    IReadOnlyList<PlanningBatchOpResult> Results,
    IReadOnlyList<string>? Warnings = null);
