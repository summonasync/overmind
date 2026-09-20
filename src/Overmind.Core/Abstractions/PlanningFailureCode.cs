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
    BriefNotAllowedForKind,
    ChangeNoteRequired,
    CannotValidateTask,
    HumanValidationRequired,
    ImportFailed
}
