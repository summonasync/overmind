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
    bool IsCurrent);

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
    PlanningArtifactResponse[] Artifacts);

public sealed record CreatePlanningArtifactRequest(
    PlanningArtifactKind Kind,
    string Body,
    int? Order,
    PlanningReviewLevel? Review,
    string? LogNote,
    string? Brief = null);

public sealed record UpdatePlanningArtifactRequest(string Body, string? LogNote, string? Brief = null);

public sealed record SetPlanningArtifactStatusRequest(PlanningArtifactStatus Status, string LogNote);

public sealed record AppendPlanningLogRequest(string Note);

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

public sealed record PlanningMaterializedFileResponse(string RelativePath, string Sha256);

public sealed record PlanningMaterializationReportResponse(
    Guid ProjectId,
    PlanningMaterialization Strategy,
    PlanningMaterializedFileResponse[] Files,
    string[] Warnings);

public sealed record PlanningDriftResponse(
    Guid ProjectId,
    string? FeatureSlug,
    string RelativePath,
    DriftKind Kind);
