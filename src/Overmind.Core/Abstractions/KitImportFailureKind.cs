namespace Overmind.Core;

public enum KitImportFailureKind
{
    PathMissing = 0,
    NotADirectory = 1,
    DuplicateName = 2,
    ReImportRequired = 3,
    CandidateNotFound = 4,
    KitNotFound = 5,
    NoSource = 6,
    SourceMissing = 7,
    BundledSourceProtected = 8,
    BundledReimportBlocked = 9
}
