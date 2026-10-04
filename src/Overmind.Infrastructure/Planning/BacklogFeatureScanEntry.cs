using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogFeatureScanEntry(
    string Folder,
    string? Slug,
    string? Title,
    PlanningFeatureStatus? Status,
    int Specs,
    int Plans,
    int Results,
    int Tasks,
    bool AlreadyImported,
    string? Error);
