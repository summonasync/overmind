using Overmind.Core;

namespace Overmind.Infrastructure.Entries;

// FeatureId is the board card the work landed on: for a feature promotion it is the created feature itself, for
// a task promotion it is the parent the task hangs under. TargetKind and TargetId name the exact artifact the
// provenance edge points at, which is what the note's rails have to render.
public sealed record TriagePromotionResult(
    Guid EntryId,
    EntryLinkKind TargetKind,
    Guid TargetId,
    Guid FeatureId,
    string FeatureSlug);
