using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record PlanningFeatureBoardItem(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    int Version,
    PlanningArtifactStatus? SpecStatus,
    PlanningArtifactStatus? PlanStatus,
    int TaskTotal,
    int TaskDone,
    DateTime UpdatedAt);

public sealed record CreatePlanningFeatureRequest(string Slug, string Title, string? Body);

public sealed record UpdatePlanningFeatureRequest(string? Title, string? Body, PlanningFeatureStatus? Status);

public sealed record PlanningFeatureResponse(
    string Slug,
    string Title,
    string Body,
    PlanningFeatureStatus Status,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlanningArtifactResponse[] Artifacts);

public sealed record CreatePlanningArtifactRequest(
    PlanningArtifactKind Kind,
    string Body,
    int? Order,
    PlanningReviewLevel? Review,
    string? LogNote);

public sealed record UpdatePlanningArtifactRequest(string Body, string? LogNote);

public sealed record SetPlanningArtifactStatusRequest(PlanningArtifactStatus Status, string LogNote);

public sealed record AppendPlanningLogRequest(string Note);

public sealed record PlanningArtifactResponse(
    Guid Id,
    PlanningArtifactKind Kind,
    PlanningArtifactStatus Status,
    PlanningReviewLevel Review,
    int Order,
    string Body,
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
