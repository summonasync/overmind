using System.Text.Json.Serialization;
using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record PlanningFeatureBoardItem(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    FeaturePriority Priority,
    int Version,
    PlanningArtifactStatus? SpecStatus,
    PlanningArtifactStatus? PlanStatus,
    int TaskTotal,
    int TaskDone,
    PlanningTaskCounts TaskCounts,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsBlocked,
    int QueuePosition,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    DateTime? ClaimedAt,
    AgentSessionLiveness? ClaimState,
    bool ClaimedByHuman,
    string Phase,
    string? BriefExcerpt,
    int OpenDirectiveCount,
    string? Lane = null,
    LaneHolderResponse? LaneHolder = null,
    PotentialOverlap[]? PotentialOverlaps = null);

public sealed record PlanningQueueItem(
    string Slug,
    string? Number,
    string Title,
    FeaturePriority Priority,
    PlanningFeatureStatus Status,
    int QueuePosition,
    PlanningArtifactStatus? SpecStatus,
    PlanningArtifactStatus? PlanStatus,
    int TaskDone,
    int TaskTotal,
    PlanningTaskCounts TaskCounts,
    int Readiness,
    string[] BlockedBy,
    bool IsBlocked,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    DateTime? ClaimedAt,
    AgentSessionLiveness? ClaimState,
    bool ClaimedByHuman,
    string Phase,
    string? BriefExcerpt,
    int OpenDirectiveCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Lane = null,
    LaneHolderResponse? LaneHolder = null,
    PotentialOverlap[]? PotentialOverlaps = null);

public sealed record PlanningTaskCounts(int Todo, int InProgress, int Review, int Done, int Blocked);

public sealed record PlanningQueueResponse(PlanningQueueItem[] Items);

public sealed record PlanningQueueMoveRequest(string? Move, int? Position);

public sealed record PlanningQueueReorderRequest(string Priority, string[] Slugs);

public sealed record CreatePlanningFeatureRequest(string Slug, string Title, string? Body, string? Priority = null);

public sealed record UpdatePlanningFeatureRequest(
    string? Title, string? Body, PlanningFeatureStatus? Status, string? Priority = null, string? Lane = null,
    string[]? CodeSurfaces = null);

public sealed record UpdatePlanningFeatureByAgentRequest(
    string? Title, string? Body, string? Priority, string LogNote, string? Lane = null,
    string[]? CodeSurfaces = null);

public sealed record SetPlanningFeaturePriorityRequest(string Priority);

public sealed record SetPlanningFeatureCodePinRequest(string? Branch, string? WorktreePath);

public sealed record PlanningFeatureCodePinResponse(
    string Slug,
    string? PinnedBranch,
    string? PinnedWorktreePath,
    int Version,
    string LastLogLine);

public sealed record ClaimPlanningFeatureRequest(string Alias, string Vendor, bool OverrideLaneCheck = false);

public sealed record PlanningFeatureClaimResponse(
    string Slug,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    DateTime? ClaimedAt,
    AgentSessionLiveness? ClaimState,
    bool ClaimedByHuman,
    string? Lane = null,
    bool OverrideLaneCheck = false);

public sealed record LaneUpsertRequest(string Name, string? Contour = null, int? SerialOrder = null);

public sealed record SetLanesRequest(LaneUpsertRequest[] Lanes);

public sealed record LaneHolderResponse(string Alias, string Vendor, AgentSessionLiveness ClaimState);

public sealed record PotentialOverlap(string WithFeature, string[] SharedGlobs);

public sealed record LaneFeatureResponse(
    string Slug,
    PlanningFeatureStatus Status,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    AgentSessionLiveness? ClaimState);

public sealed record LaneRowResponse(
    string Name,
    string? Contour,
    int? SerialOrder,
    int OpenFeatureCount,
    LaneHolderResponse? Holder,
    LaneFeatureResponse[] Features);

public sealed record LanesResponse(LaneRowResponse[] Lanes);

public sealed record SetPlanningFeatureStatusRequest(PlanningFeatureStatus Status, string LogNote);

public sealed record PlanningFeatureResponse(
    string Slug,
    string Title,
    string Body,
    PlanningFeatureStatus Status,
    FeaturePriority Priority,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlanningArtifactResponse[] Artifacts,
    bool IsBlocked,
    PlanningFeatureDependencyEntryResponse[] BlockedBy,
    PlanningFeatureDependencyEntryResponse[] Blocks,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    DateTime? ClaimedAt,
    AgentSessionLiveness? ClaimState,
    bool ClaimedByHuman,
    string? PinnedBranch,
    string? PinnedWorktreePath,
    string? Lane = null,
    string[]? CodeSurfaces = null,
    PotentialOverlap[]? PotentialOverlaps = null,
    OpenDirectiveResponse[]? OpenDirectives = null);

public sealed record OpenDirectiveResponse(
    Guid Id,
    string Text,
    string? AuthorAlias,
    string? AuthorVendor,
    DateTime CreatedAt,
    bool Ack);

public sealed record AppendPlanningDirectiveRequest(string Text);

public sealed record AppendPlanningDirectiveHumanRequest(string Text, string? Alias = null, string? Vendor = null);

public sealed record AckPlanningDirectiveRequest(string? Alias = null, string? Vendor = null);

public sealed record DirectiveAckResponse(
    Guid Id,
    bool Ack,
    string? AckByAlias,
    string? AckVendor,
    DateTime? AckAt);

public sealed record PlanningFeatureRosterResponse(
    string Slug,
    string Title,
    string Body,
    PlanningFeatureStatus Status,
    FeaturePriority Priority,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlanningArtifactRosterItemResponse[] Artifacts,
    bool IsBlocked,
    PlanningFeatureDependencyEntryResponse[] BlockedBy,
    PlanningFeatureDependencyEntryResponse[] Blocks,
    string? ClaimedByAlias,
    string? ClaimedByVendor,
    DateTime? ClaimedAt,
    AgentSessionLiveness? ClaimState,
    bool ClaimedByHuman,
    string? PinnedBranch,
    string? PinnedWorktreePath,
    int OpenDirectiveCount,
    string? Lane = null,
    string[]? CodeSurfaces = null,
    PotentialOverlap[]? PotentialOverlaps = null);

public sealed record AddPlanningFeatureDependencyRequest(string BlockedBy);

public sealed record PlanningFeatureDependencyEntryResponse(
    string Slug,
    PlanningFeatureStatus Status,
    bool Closed);

public sealed record PlanningFeatureDependenciesResponse(
    string Slug,
    PlanningFeatureDependencyEntryResponse[] BlockedBy,
    PlanningFeatureDependencyEntryResponse[] Blocks);

public sealed record CreatePlanningArtifactRequest(
    PlanningArtifactKind Kind,
    string Body,
    int? Order,
    PlanningReviewLevel? Review,
    string? LogNote,
    string? Brief = null,
    string? Source = null,
    IReadOnlyList<int>? BlockedBy = null);

public sealed record UpdatePlanningArtifactRequest(
    string Body,
    string? LogNote,
    string? Brief = null,
    string? Source = null);

public sealed record PlanningTaskUpsertRequest(
    string Body,
    int? Order = null,
    PlanningReviewLevel? Review = null,
    string? LogNote = null,
    string? Brief = null,
    IReadOnlyList<int>? BlockedBy = null);

public sealed record CreatePlanningTasksRequest(PlanningTaskUpsertRequest[] Tasks, string? Source = null);

public sealed record CreatePlanningTasksResponse(
    int Created,
    int Updated,
    PlanningArtifactResponse[] Artifacts);

public sealed record SetPlanningArtifactStatusRequest(PlanningArtifactStatus Status, string LogNote);

public sealed record AppendPlanningLogRequest(string Note, string? Source = null);

public sealed record EditPlanningLogRequest(string Text);

public sealed record SetPlanningArtifactStatusResponse(
    Guid Id,
    PlanningArtifactStatus Status,
    int Version,
    string LastLogLine,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Warnings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FeatureStatusChanged = null);

public sealed record PlanningArtifactMutationResponse(
    Guid Id,
    PlanningArtifactStatus Status,
    int Version,
    string LastLogLine);

public sealed record PlanningFeatureMutationResponse(
    string Slug,
    PlanningFeatureStatus Status,
    int Version,
    string LastLogLine);

public sealed record PlanningFeatureAgentUpdateResponse(
    string Slug,
    PlanningFeatureStatus Status,
    int Version,
    DateTime UpdatedAt,
    int BodyChars);

public sealed record ValidatePlanningArtifactRequest(bool? AsMigrated = null, string? LogNote = null);

public sealed record DeclinePlanningArtifactRequest(string? Reason = null);

public sealed record DeletePlanningArtifactRequest(
    string? LogNote = null,
    string? Source = null);

public sealed record PlanningArtifactDeleteResponse(
    Guid Id,
    PlanningArtifactKind Kind,
    string FeatureLogLine);

public sealed record PatchPlanningArtifactRequest(
    string? Op,
    string? Section,
    string? Content,
    int? AcIndex,
    string? AcText,
    string? Brief,
    string? LogNote = null,
    string? Source = null);

public sealed record PlanningArtifactPatchResponse(
    Guid ArtifactId,
    PlanningArtifactKind Kind,
    string Section,
    int Version,
    int BodyChars,
    PlanningArtifactStatus Status,
    string LastLogLine,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StatusChanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FeatureStatusChanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TickedIndex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TickedText = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? NoChange = null);

public sealed record PlanningArtifactReadResponse(
    Guid Id,
    PlanningArtifactKind Kind,
    PlanningArtifactStatus Status,
    int Version,
    int Order,
    DateTime UpdatedAt,
    string? Brief,
    string Body,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? MissingSections = null);

public sealed record PlanningArtifactRosterResponse(PlanningArtifactRosterItemResponse[] Artifacts);

public sealed record PlanningArtifactRosterItemResponse(
    Guid ArtifactId,
    PlanningArtifactKind Kind,
    PlanningArtifactStatus Status,
    int Version,
    int Order,
    DateTime UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlanningReviewLevel? Review = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AcTicked = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AcTotal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Brief = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Blocked = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int[]? BlockedBy = null,
    PlanningArtifactPointerResponse[] Pointers = null!);

public sealed record PlanningArtifactResponse(
    Guid Id,
    PlanningArtifactKind Kind,
    PlanningArtifactStatus Status,
    PlanningReviewLevel Review,
    int Order,
    string Body,
    string? Brief,
    DateTime? FrozenAt,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlanningLogLineResponse[] Log,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StatusChanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FeatureStatusChanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Blocked = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int[]? BlockedBy = null,
    PlanningArtifactPointerResponse[] Pointers = null!);

public sealed record PlanningLogLineResponse(string? Date, string Text);

public sealed record PlanningArtifactPointerResponse(string Path, string? Anchor);

public sealed record PlanningArtifactVersionResponse(int Version, string Body, DateTime CreatedAt);

public sealed record PlanningArtifactDiffResponse(
    Guid ArtifactId,
    int FromVersion,
    int ToVersion,
    string Diff);

public sealed record PlanningWorkflowResponse(
    PlanningMaterialization Materialization,
    HumanValidationOverride RequireHumanValidation,
    PlanningMaterialization EffectiveMaterialization,
    bool EffectiveRequireHumanValidation);

public sealed record UpdatePlanningWorkflowRequest(
    string? Materialization,
    string? RequireHumanValidation);

public sealed record PlanningCurrentResponse(
    string State,
    PlanningCurrentFeature Feature,
    PlanningArtifactResponse? Artifact,
    string? Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Warnings = null);

public sealed record PlanningCurrentFeature(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    OpenDirectiveResponse[]? OpenDirectives = null);

public sealed record MaterializePlanningRequest(bool Force);

public sealed record ImportPlanningFeatureRequest(string Path);

public sealed record PlanningFeatureImportResponse(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    bool Created,
    int ArtifactsCreated,
    int ArtifactsUpdated,
    string[] Notes,
    int TrackedFiles,
    PlanningMaterialization Materialization,
    string? NextStep);

public sealed record PlanningImportFolderRequest(string Folder);

public sealed record PlanningImportScanRequest(string? SubPath);

public sealed record PlanningImportScanEntryResponse(
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

public sealed record PlanningImportScanResponse(string Root, PlanningImportScanEntryResponse[] Entries, string? ManifestHint);

public sealed record PlanningMaterializedFileResponse(string RelativePath, string Sha256);

public sealed record PlanningMaterializationSkipResponse(string RelativePath, string Reason);

public sealed record PlanningMaterializationReportResponse(
    Guid ProjectId,
    PlanningMaterialization Strategy,
    PlanningMaterializedFileResponse[] Files,
    string[] Warnings,
    PlanningMaterializationSkipResponse[] Skipped,
    DateTime? LastSuccess,
    DateTime? LastAttempt,
    PlanningGitHygieneOutcomeResponse? GitHygiene = null);

public sealed record PlanningGitHygieneOutcomeResponse(string Status, string? RepositoryRoot, string? Detail);

public sealed record PlanningMaterializationStatusRunResponse(
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Atomic,
    string[] Written,
    PlanningMaterializationSkipResponse[] Skipped,
    PlanningMaterializationSkipResponse[] Refused);

public sealed record PlanningMaterializationStatusResponse(
    Guid ProjectId,
    DateTime? LastSuccess,
    DateTime? LastAttempt,
    string? LastAttemptReason,
    PlanningMaterializationStatusRunResponse? LastRun,
    int TrackedFiles,
    string[] StalePaths,
    PlanningMaterialization Strategy,
    string TargetDir);

public sealed record PlanningDriftResponse(
    Guid ProjectId,
    string? FeatureSlug,
    string RelativePath,
    DriftKind Kind);

public sealed record PlanningExportRequest(string TargetPath);

public sealed record PlanningExportResponse(Guid ProjectId, string Target, int FileCount);

public sealed record WaveSnapshotResponse(
    Guid ProjectId,
    DateTime GeneratedAt,
    string[] CoordinationTags,
    PlanningQueueItem[] Queue,
    WaveSnapshotClaim[] Claims,
    WaveSnapshotCodeLocation[] CodeLocations,
    WaveSnapshotBlockers[] OpenBlockers,
    WaveSnapshotEntry[] Entries);

public sealed record WaveSnapshotClaim(
    string Slug,
    string ClaimedByAlias,
    string ClaimedByVendor,
    DateTime ClaimedAt,
    AgentSessionLiveness ClaimState,
    bool ClaimedByHuman,
    DateTime LastActivityAt,
    int QueuePosition,
    FeaturePriority Priority,
    PlanningFeatureStatus Status);

public sealed record WaveSnapshotCodeLocation(string Slug, string? PinnedBranch, string? PinnedWorktreePath);

public sealed record WaveSnapshotBlockers(string Slug, string[] OpenBlockers);

public sealed record WaveSnapshotEntry(
    Guid Id,
    EntryType Type,
    string Title,
    string[] Tags,
    string Snippet,
    DateTime UpdatedAt);
