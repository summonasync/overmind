using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

// The note's text is never the artifact's text: promotion copies nothing, it writes a manual edge and leaves the
// note exactly as it was. So the body here is the card's body, authored by the owner at the moment of the act.
public sealed record PromoteNoteToFeatureRequest(
    Guid ProjectId,
    string Title,
    string Body,
    string? Priority = null);

public sealed record PromoteNoteToTaskRequest(
    Guid FeatureId,
    string Body,
    string? Brief = null);

// TargetKind and TargetId name the artifact the provenance edge points at; FeatureId and FeatureSlug name the board
// card the web layer invalidates, which for a task promotion is the parent rather than the task.
public sealed record NotePromotionResponse(
    Guid EntryId,
    EntryLinkKind TargetKind,
    Guid TargetId,
    Guid FeatureId,
    string FeatureSlug);
