using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class ConversationReadStore
{
    private readonly OvermindDbContext _db;

    public ConversationReadStore(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation> MarkReadAsync(
        Guid conversationId, Guid lastReadMessageId, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            ?? throw PlanningException.MissingConversation(conversationId);

        await RequireMessageAsync(conversationId, lastReadMessageId, ct);

        if (conversation.LastReadMessageId is { } current && current != lastReadMessageId)
        {
            var readAt = await _db.ConversationMessages
                .AsNoTracking()
                .Where(m => m.Id == current)
                .Select(m => (DateTime?)m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (readAt is { } watermarkAt && !await IsAheadAsync(lastReadMessageId, watermarkAt, current, ct))
            {
                return conversation;
            }
        }

        conversation.MarkRead(lastReadMessageId);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task<int> CountUnreadAsync(CancellationToken ct = default)
    {
        var unread =
            from conversation in _db.Conversations.AsNoTracking()
            join message in _db.ConversationMessages.AsNoTracking()
                on conversation.Id equals message.ConversationId
            join readRow in _db.ConversationMessages.AsNoTracking()
                on conversation.LastReadMessageId equals (Guid?)readRow.Id into readRows
            from readRow in readRows.DefaultIfEmpty()
            where message.Role == ConversationMessage.AssistantRole
            where conversation.LastReadMessageId == null
                || message.CreatedAt > readRow!.CreatedAt
                || (message.CreatedAt == readRow!.CreatedAt && message.Id > conversation.LastReadMessageId.Value)
            select message.Id;

        return await unread.CountAsync(ct);
    }

    private async Task RequireMessageAsync(Guid conversationId, Guid messageId, CancellationToken ct)
    {
        var exists = await _db.ConversationMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId && m.ConversationId == conversationId, ct);
        if (!exists)
        {
            throw PlanningException.MissingConversationMessage(messageId, conversationId);
        }
    }

    private async Task<bool> IsAheadAsync(Guid messageId, DateTime watermarkAt, Guid watermarkId, CancellationToken ct)
    {
        return await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Where(m => m.CreatedAt > watermarkAt
                || (m.CreatedAt == watermarkAt && m.Id > watermarkId))
            .AnyAsync(ct);
    }
}
