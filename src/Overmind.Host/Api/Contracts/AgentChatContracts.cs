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

public sealed record ConversationHistoryResponse(ConversationMessageResponse[] Items);

public sealed record ConversationReadStateResponse(Guid ConversationId, Guid? LastReadMessageId);

public sealed record UnreadCountResponse(int Count);
