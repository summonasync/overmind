using System.Text.Json.Serialization;

namespace Overmind.Host.Api.Contracts;

public sealed record CreateConversationRequest(Guid? ProjectId, string? Title);

public sealed record RenameConversationRequest(string Title);

public sealed record SendConversationMessageRequest(string Text);

public sealed record MarkConversationReadRequest(Guid LastReadMessageId);

public sealed record ConversationResponse(
    Guid Id,
    Guid? ProjectId,
    string? Title,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ConversationsResponse(ConversationResponse[] Items);

public sealed record ConversationMessageResponse(Guid Id, string Role, string Content, DateTime CreatedAt);

// TurnActive says the Host is still generating this conversation's reply: a reload lands here
// with no live SSE attached, so the client keeps refetching until it flips false — the persisted
// reply then shows up without waiting for the next send (b88c1bae).
public sealed record ConversationHistoryResponse(
    ConversationMessageResponse[] Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool TurnActive = false);

public sealed record ConversationReadStateResponse(Guid ConversationId, Guid? LastReadMessageId);

public sealed record ConversationTurnStopResponse(bool Stopped);

public sealed record UnreadCountResponse(int Count);
