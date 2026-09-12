namespace Overmind.Core;

public enum PlanningFailureCode
{
    MissingProject,
    MissingFeature,
    MissingArtifact,
    DuplicateSlug,
    DuplicateArtifact,
    InvalidSlug,
    InvalidStatusForKind,
    ChangeNoteRequired,
    CannotValidateTask,
    HumanValidationRequired
}
