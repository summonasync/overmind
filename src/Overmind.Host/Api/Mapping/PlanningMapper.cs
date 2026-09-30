using System.Text.RegularExpressions;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api.Mapping;

public static class PlanningMapper
{
    private static readonly Regex FeatureNumberPattern = new(@"^([0-9]+[a-z]?)(?:-|$)", RegexOptions.Compiled);

    public static PlanningFeatureBoardItem ToBoardItem(PlanningFeature feature, bool isBlocked)
    {
        var artifacts = feature.Artifacts;

        return new PlanningFeatureBoardItem(
            feature.Slug,
            feature.Title,
            feature.Status,
            feature.Priority,
            feature.Version,
            ArtifactStatus(artifacts, PlanningArtifactKind.Spec),
            ArtifactStatus(artifacts, PlanningArtifactKind.Plan),
            TaskTotal(artifacts),
            TaskDone(artifacts),
            feature.UpdatedAt,
            feature.IsCurrent,
            isBlocked,
            feature.QueuePosition);
    }

    public static PlanningQueueItem ToQueueItem(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures)
    {
        var blockers = OpenBlockerSlugs(feature, projectFeatures);
        var artifacts = feature.Artifacts;

        return new PlanningQueueItem(
            feature.Slug,
            FeatureNumber(feature.Slug),
            feature.Title,
            feature.Priority,
            feature.Status,
            feature.QueuePosition,
            ArtifactStatus(artifacts, PlanningArtifactKind.Spec),
            ArtifactStatus(artifacts, PlanningArtifactKind.Plan),
            TaskDone(artifacts),
            TaskTotal(artifacts),
            Readiness(feature),
            blockers,
            blockers.Length > 0,
            feature.IsCurrent);
    }

    public static int Readiness(PlanningFeature feature) =>
        (ArtifactStatus(feature.Artifacts, PlanningArtifactKind.Spec) == PlanningArtifactStatus.Validated ? 1 : 0) +
        (ArtifactStatus(feature.Artifacts, PlanningArtifactKind.Plan) == PlanningArtifactStatus.Validated ? 1 : 0) +
        (TaskTotal(feature.Artifacts) > 0 ? 1 : 0);

    public static string? FeatureNumber(string slug)
    {
        var match = FeatureNumberPattern.Match(slug);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string[] OpenBlockerSlugs(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures) =>
        [.. feature.BlockedBy
            .Select(link => projectFeatures.TryGetValue(link.BlockedByFeatureId, out var blocker) ? blocker : null)
            .Where(b => b is not null && !FeatureDependencyEntry.IsClosed(b!.Status))
            .Select(b => b!.Slug)
            .OrderBy(s => s, StringComparer.Ordinal)];

    private static PlanningArtifactStatus? ArtifactStatus(
        IReadOnlyCollection<PlanningArtifact> artifacts, PlanningArtifactKind kind) =>
        artifacts.FirstOrDefault(a => a.Kind == kind)?.Status;

    private static int TaskTotal(IReadOnlyCollection<PlanningArtifact> artifacts) =>
        artifacts.Count(a => a.Kind == PlanningArtifactKind.Task);

    private static int TaskDone(IReadOnlyCollection<PlanningArtifact> artifacts) =>
        artifacts.Count(a => a.Kind == PlanningArtifactKind.Task && a.Status == PlanningArtifactStatus.Done);

    public static bool HasOpenBlocker(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures) =>
        feature.BlockedBy.Any(link =>
            projectFeatures.TryGetValue(link.BlockedByFeatureId, out var blocker) &&
            !FeatureDependencyEntry.IsClosed(blocker.Status));

    public static PlanningFeatureResponse ToFeatureResponse(
        PlanningFeature feature, FeatureDependencies dependencies,
        IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null)
    {
        return new PlanningFeatureResponse(
            feature.Slug,
            feature.Title,
            feature.Body,
            feature.Status,
            feature.Priority,
            feature.Version,
            feature.CreatedAt,
            feature.UpdatedAt,
            feature.Artifacts
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.Order)
                .Select(a => ToArtifactResponse(a, blockedStates))
                .ToArray(),
            dependencies.BlockedBy.Any(entry => !entry.Closed),
            [.. dependencies.BlockedBy.Select(ToDependencyEntryResponse)],
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)]);
    }

    public static PlanningFeatureRosterResponse ToFeatureRosterResponse(
        PlanningFeature feature, FeatureDependencies dependencies,
        IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null)
    {
        return new PlanningFeatureRosterResponse(
            feature.Slug,
            feature.Title,
            feature.Body,
            feature.Status,
            feature.Priority,
            feature.Version,
            feature.CreatedAt,
            feature.UpdatedAt,
            feature.Artifacts
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.Order)
                .Select(a => ToRosterItem(a, blockedStates))
                .ToArray(),
            dependencies.BlockedBy.Any(entry => !entry.Closed),
            [.. dependencies.BlockedBy.Select(ToDependencyEntryResponse)],
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)]);
    }

    public static PlanningFeatureDependenciesResponse ToDependenciesResponse(FeatureDependencies dependencies)
    {
        return new PlanningFeatureDependenciesResponse(
            dependencies.Slug,
            [.. dependencies.BlockedBy.Select(ToDependencyEntryResponse)],
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)]);
    }

    private static PlanningFeatureDependencyEntryResponse ToDependencyEntryResponse(
        FeatureDependencyEntry entry) =>
        new(entry.Slug, entry.Status, entry.Closed);

    public static PlanningArtifactRosterItemResponse ToRosterItem(
        PlanningArtifact artifact, IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null)
    {
        if (artifact.Kind != PlanningArtifactKind.Task)
        {
            return new(
                artifact.Id, artifact.Kind, artifact.Status,
                artifact.Version, artifact.Order, artifact.UpdatedAt);
        }

        var (blocked, blockedBy) = BlockedView(artifact, blockedStates);
        var items = PlanningSections.ParseAcceptanceCriteria(artifact.Body);
        return new(
            artifact.Id, artifact.Kind, artifact.Status,
            artifact.Version, artifact.Order, artifact.UpdatedAt,
            artifact.Review,
            items.Count(i => i.Checked == true),
            items.Count,
            artifact.Brief,
            blocked,
            blockedBy);
    }

    public static PlanningArtifactResponse ToArtifactResponse(
        PlanningArtifact artifact, IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null)
    {
        var (blocked, blockedBy) = BlockedView(artifact, blockedStates);
        return new PlanningArtifactResponse(
            artifact.Id,
            artifact.Kind,
            artifact.Status,
            artifact.Review,
            artifact.Order,
            artifact.Body,
            artifact.Brief,
            artifact.FrozenAt,
            artifact.Version,
            artifact.CreatedAt,
            artifact.UpdatedAt,
            PlanningLog.Parse(artifact.Body)
                .Select(l => new PlanningLogLineResponse(
                    l.Date?.ToString("yyyy-MM-dd"),
                    l.Text))
                .ToArray(),
            artifact.StatusChanged,
            artifact.FeatureStatusChanged,
            blocked,
            blockedBy);
    }

    private static (bool? Blocked, int[]? BlockedBy) BlockedView(
        PlanningArtifact artifact, IReadOnlyDictionary<int, TaskBlockedState>? blockedStates)
    {
        if (artifact.Kind != PlanningArtifactKind.Task ||
            blockedStates is null ||
            !blockedStates.TryGetValue(artifact.Order, out var state) ||
            state.BlockedBy.Count == 0)
        {
            return (null, null);
        }

        return (state.Blocked, [.. state.BlockedBy]);
    }

    public static PlanningArtifactVersionResponse ToVersionResponse(PlanningArtifactVersion version)
    {
        return new PlanningArtifactVersionResponse(version.Version, version.Body, version.CreatedAt);
    }

    public static PlanningArtifactMutationResponse ToMutationResponse(PlanningArtifact artifact)
    {
        return new PlanningArtifactMutationResponse(
            artifact.Id, artifact.Status, artifact.Version, artifact.LastLogLine);
    }

    public static PlanningFeatureMutationResponse ToMutationResponse(PlanningFeature feature)
    {
        return new PlanningFeatureMutationResponse(
            feature.Slug, feature.Status, feature.Version, feature.LastLogLine);
    }
}
