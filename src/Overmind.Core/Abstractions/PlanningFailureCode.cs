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
    InvalidFeatureTransition,
    BriefNotAllowedForKind,
    ChangeNoteRequired,
    FeatureLogNoteRequired,
    FeatureStatusNotAllowedForAgent,
    CannotValidateTask,
    HumanValidationRequired,
    ImportFailed,
    PatchSectionNotFound,
    TickItemNotFound,
    TickTextAmbiguous,
    TickItemNotCheckable,
    InvalidPatchRequest,
    PatchLogSectionNotReplaceable
}
