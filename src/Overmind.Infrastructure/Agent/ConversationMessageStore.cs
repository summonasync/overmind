using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class ConversationMessageStore
{
    public const int DefaultHistoryLimit = 50;
    public const int MaxHistoryLimit = 500;

    private readonly OvermindDbContext _db;

    public ConversationMessageStore(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationMessage> AppendAsync(
        Guid conversationId, string role, string? content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var trimmed = content.Trim();
        if (trimmed.Length > ConversationMessage.MaxTextLength)
        {
            throw PlanningException.ConversationTextTooLong(trimmed.Length, ConversationMessage.MaxTextLength);
        }

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            ?? throw PlanningException.MissingConversation(conversationId);

        var message = ConversationMessage.Create(conversationId, role, content);
        _db.ConversationMessages.Add(message);
        conversation.Touch();
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<ConversationMessage>> ListPageAsync(
        Guid conversationId, int? limit = null, DateTime? before = null, CancellationToken ct = default)
    {
        var capped = Math.Clamp(limit ?? DefaultHistoryLimit, 1, MaxHistoryLimit);

        var page = await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Where(m => before == null || m.CreatedAt < before)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(capped)
            .ToListAsync(ct);

        page.Reverse();
        return page;
    }
}
