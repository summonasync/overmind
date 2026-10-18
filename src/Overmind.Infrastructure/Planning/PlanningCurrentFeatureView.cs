using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningCurrentFeatureView(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    IReadOnlyList<PlanningDirective> OpenDirectives);
