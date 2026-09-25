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
    DateTime UpdatedAt,
    bool IsCurrent,
    bool IsBlocked,
    int QueuePosition);

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
    int Readiness,
    string[] BlockedBy,
    bool IsBlocked,
    bool IsCurrent);

public sealed record PlanningQueueResponse(PlanningQueueItem[] Items);

public sealed record PlanningQueueMoveRequest(string? Move, int? Position);

public sealed record PlanningQueueReorderRequest(string Priority, string[] Slugs);

public sealed record CreatePlanningFeatureRequest(string Slug, string Title, string? Body, string? Priority = null);

public sealed record UpdatePlanningFeatureRequest(
    string? Title, string? Body, PlanningFeatureStatus? Status, string? Priority = null);

public sealed record SetPlanningFeatureCurrentRequest(bool IsCurrent);

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
    PlanningFeatureDependencyEntryResponse[] Blocks);

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
    string? Brief = null);

public sealed record UpdatePlanningArtifactRequest(string Body, string? LogNote, string? Brief = null);

public sealed record PlanningTaskUpsertRequest(
    string Body,
    int? Order = null,
    PlanningReviewLevel? Review = null,
    string? LogNote = null,
    string? Brief = null);

public sealed record CreatePlanningTasksRequest(PlanningTaskUpsertRequest[] Tasks);

public sealed record CreatePlanningTasksResponse(
    int Created,
    int Updated,
    PlanningArtifactResponse[] Artifacts);

public sealed record SetPlanningArtifactStatusRequest(PlanningArtifactStatus Status, string LogNote);

public sealed record AppendPlanningLogRequest(string Note);

public sealed record SetPlanningArtifactStatusResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Warnings = null);

public sealed record ValidatePlanningArtifactRequest(bool? AsMigrated = null, string? LogNote = null);

public sealed record PatchPlanningArtifactRequest(
    string? Op,
    string? Section,
    string? Content,
    int? AcIndex,
    string? AcText,
    string? LogNote = null);

public sealed record PlanningArtifactPatchResponse(
    Guid ArtifactId,
    PlanningArtifactKind Kind,
    string Section,
    int Version,
    int BodyChars);

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
    PlanningLogLineResponse[] Log);

public sealed record PlanningLogLineResponse(string? Date, string Text);

public sealed record PlanningArtifactVersionResponse(int Version, string Body, DateTime CreatedAt);

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
    string? Reason);

public sealed record PlanningCurrentFeature(
    string Slug,
    string Title,
    PlanningFeatureStatus Status);

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

public sealed record PlanningMaterializedFileResponse(string RelativePath, string Sha256);

public sealed record PlanningMaterializationSkipResponse(string RelativePath, string Reason);

public sealed record PlanningMaterializationReportResponse(
    Guid ProjectId,
    PlanningMaterialization Strategy,
    PlanningMaterializedFileResponse[] Files,
    string[] Warnings,
    PlanningMaterializationSkipResponse[] Skipped,
    DateTime? LastSuccess,
    DateTime? LastAttempt);

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
