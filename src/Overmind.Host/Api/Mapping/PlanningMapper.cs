using System.Globalization;
using System.Text.RegularExpressions;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api.Mapping;

public static class PlanningMapper
{
    private static readonly Regex FeatureNumberPattern = new(@"^([0-9]+[a-z]?)(?:-|$)", RegexOptions.Compiled);

    public static PlanningFeatureBoardItem ToBoardItem(
        PlanningFeature feature, bool isBlocked, AgentSessionLiveness? claimState = null,
        LaneHolderResponse? laneHolder = null, IReadOnlyList<FeatureOverlapRow>? overlaps = null,
        int openDirectiveCount = 0)
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
            TaskCounts(artifacts),
            feature.CreatedAt,
            feature.UpdatedAt,
            isBlocked,
            feature.QueuePosition,
            feature.ClaimedByAlias,
            feature.ClaimedByVendor,
            feature.ClaimedAt,
            feature.ClaimSessionId is null ? null : claimState,
            feature.ClaimedByHuman,
            CeremonyPhasePlanner.DeriveLabel(
                feature.Status,
                ArtifactStatus(artifacts, PlanningArtifactKind.Spec),
                ArtifactStatus(artifacts, PlanningArtifactKind.Plan),
                TaskTotal(artifacts)),
            CeremonyPhasePlanner.BriefExcerpt(feature.Body),
            openDirectiveCount,
            feature.Lane,
            laneHolder ?? LaneHolderOf(feature, claimState),
            Overlaps(overlaps));
    }

    public static PlanningQueueItem ToQueueItem(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures,
        AgentSessionLiveness? claimState = null, LaneHolderResponse? laneHolder = null,
        IReadOnlyList<FeatureOverlapRow>? overlaps = null,
        int openDirectiveCount = 0)
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
            TaskCounts(artifacts),
            Readiness(feature),
            blockers,
            blockers.Length > 0,
            feature.ClaimedByAlias,
            feature.ClaimedByVendor,
            feature.ClaimedAt,
            feature.ClaimSessionId is null ? null : claimState,
            feature.ClaimedByHuman,
            CeremonyPhasePlanner.DeriveLabel(
                feature.Status,
                ArtifactStatus(artifacts, PlanningArtifactKind.Spec),
                ArtifactStatus(artifacts, PlanningArtifactKind.Plan),
                TaskTotal(artifacts)),
            CeremonyPhasePlanner.BriefExcerpt(feature.Body),
            openDirectiveCount,
            feature.CreatedAt,
            feature.UpdatedAt,
            feature.Lane,
            laneHolder ?? LaneHolderOf(feature, claimState),
            Overlaps(overlaps));
    }

    public static OpenDirectiveResponse[] ToOpenDirectives(
        IEnumerable<PlanningDirective> directives) =>
        [.. directives.Select(d => new OpenDirectiveResponse(
            d.Id, d.Text, d.AuthorAlias, d.AuthorVendor, d.CreatedAt, d.Acked))];

    private static PotentialOverlap[] Overlaps(IReadOnlyList<FeatureOverlapRow>? rows) =>
        rows is null || rows.Count == 0
            ? []
            : [.. rows.Select(r => new PotentialOverlap(r.WithFeatureSlug, [.. r.SharedGlobs]))];

    private static CodeSurfaceFeatureRow ToSurfaceRow(PlanningFeature feature) =>
        new(feature.Id, feature.Slug, feature.Status, feature.ClaimSessionId, feature.CodeSurfaces);

    public static IReadOnlyDictionary<string, LaneHolderResponse> LaneHoldersByLane(
        IEnumerable<PlanningFeature> features,
        IReadOnlyDictionary<Guid, AgentSessionLiveness> claimLiveness) =>
        features
            .Where(f => f.Lane is not null &&
                        f.ClaimSessionId is not null &&
                        f.ClaimedByAlias is not null &&
                        claimLiveness.GetValueOrDefault(f.Id) == AgentSessionLiveness.Alive)
            .GroupBy(f => f.Lane!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(f => f.ClaimedAt)
                    .Select(f => new LaneHolderResponse(
                        f.ClaimedByAlias!, f.ClaimedByVendor!, AgentSessionLiveness.Alive))
                    .First(),
                StringComparer.OrdinalIgnoreCase);

    private static LaneHolderResponse? LaneHolderOf(PlanningFeature feature, AgentSessionLiveness? claimState) =>
        claimState == AgentSessionLiveness.Alive && feature.ClaimedByAlias is not null
            ? new LaneHolderResponse(feature.ClaimedByAlias, feature.ClaimedByVendor!, claimState.Value)
            : null;

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

    private static PlanningTaskCounts TaskCounts(IReadOnlyCollection<PlanningArtifact> artifacts)
    {
        var statuses = artifacts
            .Where(a => a.Kind == PlanningArtifactKind.Task)
            .Select(a => a.Status)
            .ToList();

        return new(
            statuses.Count(s => s == PlanningArtifactStatus.Todo),
            statuses.Count(s => s == PlanningArtifactStatus.InProgress),
            statuses.Count(s => s == PlanningArtifactStatus.Review),
            statuses.Count(s => s == PlanningArtifactStatus.Done),
            statuses.Count(s => s == PlanningArtifactStatus.Blocked));
    }

    public static bool HasOpenBlocker(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures) =>
        feature.BlockedBy.Any(link =>
            projectFeatures.TryGetValue(link.BlockedByFeatureId, out var blocker) &&
            !FeatureDependencyEntry.IsClosed(blocker.Status));

    public static WaveSnapshotResponse ToWaveSnapshotResponse(WaveSnapshotData snapshot)
    {
        var graph = snapshot.QueueFeatures.ToDictionary(f => f.Id);
        var laneHolders = LaneHoldersByLane(snapshot.QueueFeatures, snapshot.ClaimLiveness);
        var overlaps = CodeSurfaceOverlapProjection.Compute([.. snapshot.QueueFeatures.Select(ToSurfaceRow)]);

        return new WaveSnapshotResponse(
            snapshot.ProjectId,
            snapshot.GeneratedAt,
            [.. snapshot.CoordinationTags],
            [.. snapshot.QueueFeatures.Select(f =>
                ToQueueItem(
                    f, graph, snapshot.ClaimLiveness.GetValueOrDefault(f.Id),
                    laneHolders.GetValueOrDefault(f.Lane ?? string.Empty),
                    overlaps.GetValueOrDefault(f.Id),
                    snapshot.OpenDirectiveCounts.GetValueOrDefault(f.Id)))],
            [.. snapshot.QueueFeatures
                .Where(f => f.ClaimSessionId is not null)
                .Select(f => ToSnapshotClaim(f, snapshot.ClaimLiveness.GetValueOrDefault(f.Id)))],
            [.. snapshot.InFlightFeatures.Select(f =>
                new WaveSnapshotCodeLocation(f.Slug, f.PinnedBranch, f.PinnedWorktreePath))],
            [.. snapshot.QueueFeatures
                .Select(f => (Slug: f.Slug, Open: OpenBlockerSlugs(f, graph)))
                .Where(row => row.Open.Length > 0)
                .Select(row => new WaveSnapshotBlockers(row.Slug, row.Open))],
            [.. snapshot.CoordinationEntries.Select(ToSnapshotEntry)]);
    }

    private static WaveSnapshotClaim ToSnapshotClaim(PlanningFeature feature, AgentSessionLiveness claimState) =>
        new(
            feature.Slug,
            feature.ClaimedByAlias!,
            feature.ClaimedByVendor!,
            feature.ClaimedAt!.Value,
            claimState,
            feature.ClaimedByHuman,
            feature.UpdatedAt,
            feature.QueuePosition,
            feature.Priority,
            feature.Status);

    private static WaveSnapshotEntry ToSnapshotEntry(ContextEntry entry) =>
        new(
            entry.Id,
            entry.Type,
            entry.Title,
            [.. entry.EntryTags
                .Where(t => t.Tag is not null)
                .Select(t => t.Tag!.Name)
                .OrderBy(name => name, StringComparer.Ordinal)],
            Clip(entry.Content),
            entry.UpdatedAt);

    private static string Clip(string content)
    {
        const int cap = 200;
        var flattened = string.Join(' ', content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0));

        var info = new StringInfo(flattened);
        return info.LengthInTextElements <= cap
            ? flattened
            : info.SubstringByTextElements(0, cap) + "…";
    }

    public static PlanningFeatureResponse ToFeatureResponse(
        PlanningFeature feature, FeatureDependencies dependencies,
        IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null,
        AgentSessionLiveness? claimState = null,
        string[]? codeSurfaces = null,
        IReadOnlyList<FeatureOverlapRow>? overlaps = null,
        IReadOnlyList<PlanningDirective>? openDirectives = null)
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
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)],
            feature.ClaimedByAlias,
            feature.ClaimedByVendor,
            feature.ClaimedAt,
            feature.ClaimSessionId is null ? null : claimState,
            feature.ClaimedByHuman,
            feature.PinnedBranch,
            feature.PinnedWorktreePath,
            feature.Lane,
            codeSurfaces ?? CodeSurfacesJson.Deserialize(feature.CodeSurfaces).ToArray(),
            Overlaps(overlaps),
            ToOpenDirectives(openDirectives ?? []));
    }

    public static PlanningFeatureRosterResponse ToFeatureRosterResponse(
        PlanningFeature feature, FeatureDependencies dependencies,
        IReadOnlyDictionary<int, TaskBlockedState>? blockedStates = null,
        AgentSessionLiveness? claimState = null,
        string[]? codeSurfaces = null,
        IReadOnlyList<FeatureOverlapRow>? overlaps = null,
        int openDirectiveCount = 0)
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
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)],
            feature.ClaimedByAlias,
            feature.ClaimedByVendor,
            feature.ClaimedAt,
            feature.ClaimSessionId is null ? null : claimState,
            feature.ClaimedByHuman,
            feature.PinnedBranch,
            feature.PinnedWorktreePath,
            openDirectiveCount,
            feature.Lane,
            codeSurfaces ?? CodeSurfacesJson.Deserialize(feature.CodeSurfaces).ToArray(),
            Overlaps(overlaps));
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
        var pointers = Pointers(artifact);
        if (artifact.Kind != PlanningArtifactKind.Task)
        {
            return new(
                artifact.Id, artifact.Kind, artifact.Status,
                artifact.Version, artifact.Order, artifact.UpdatedAt,
                Pointers: pointers);
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
            blockedBy,
            pointers);
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
            blockedBy,
            Pointers(artifact),
            ValidationProofVoided: artifact.ValidationProofVoided);
    }

    private static PlanningArtifactPointerResponse[] Pointers(PlanningArtifact artifact) =>
        [.. PointerMarkerGrammar.Parse(artifact.Body)
            .Select(p => new PlanningArtifactPointerResponse(p.Path, p.Anchor))];

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

    public static PlanningFeatureImportResponse ToImportResponse(BacklogFeatureImportReport report)
    {
        return new PlanningFeatureImportResponse(
            report.Slug,
            report.Title,
            report.Status,
            report.Created,
            report.ArtifactsCreated,
            report.ArtifactsUpdated,
            [.. report.Notes],
            report.TrackedFiles,
            report.Materialization,
            report.NextStep);
    }

    public static PlanningImportScanResponse ToScanResponse(BacklogFeatureScanReport report)
    {
        return new PlanningImportScanResponse(
            report.Root,
            [.. report.Entries.Select(ToScanEntryResponse)],
            null);
    }

    private static PlanningImportScanEntryResponse ToScanEntryResponse(BacklogFeatureScanEntry entry)
    {
        return new PlanningImportScanEntryResponse(
            entry.Folder,
            entry.Slug,
            entry.Title,
            entry.Status,
            entry.Specs,
            entry.Plans,
            entry.Results,
            entry.Tasks,
            entry.AlreadyImported,
            entry.Error);
    }
}
