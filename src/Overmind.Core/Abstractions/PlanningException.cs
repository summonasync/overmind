namespace Overmind.Core;

public sealed class PlanningException : Exception
{
    private PlanningException(PlanningFailureKind kind, PlanningFailureCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        Code = code;
    }

    public PlanningFailureKind Kind { get; }

    public PlanningFailureCode Code { get; }

    public static PlanningException MissingProject(Guid projectId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingProject, $"Project '{projectId}' was not found.");

    public static PlanningException MissingFeature(string identifier) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingFeature, $"Feature '{identifier}' was not found.");

    public static PlanningException MissingArtifact(Guid artifactId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingArtifact, $"Artifact '{artifactId}' was not found.");

    public static PlanningException DuplicateSlug(string slug) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateSlug, $"A feature with slug '{slug}' already exists in this project.");

    public static PlanningException DuplicateArtifact(PlanningArtifactKind kind, int order) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateArtifact, $"An artifact of kind '{kind}' with order {order} already exists.");

    public static PlanningException InvalidSlug(string slug) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.InvalidSlug, $"Slug '{slug}' does not match the required pattern (lowercase alphanumeric with hyphens, max 64 chars).");

    public static PlanningException ChangeNoteRequired() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ChangeNoteRequired, "A change note is required to modify a frozen artifact.");

    public static PlanningException CannotValidateTask() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.CannotValidateTask, "Task artifacts cannot be validated.");

    public static PlanningException InvalidStatusForKind(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.InvalidStatusForKind, $"Status '{status}' is not allowed for artifact kind '{kind}'.");
}
