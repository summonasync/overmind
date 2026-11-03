using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramChatStore(OvermindDbContext db)
{
    public const string ConversationTitle = "Telegram";

    public async Task<Conversation> GetOrCreateConversationAsync(long chatId, CancellationToken ct = default)
    {
        var mapping = await db.TelegramChatMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChatId == chatId, ct);
        if (mapping is not null)
        {
            var conversation = await db.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == mapping.ConversationId, ct);
            if (conversation is not null)
            {
                return conversation;
            }

            await db.TelegramChatMappings.Where(m => m.ChatId == chatId).ExecuteDeleteAsync(ct);
        }

        var created = Conversation.Create(null, ConversationTitle);
        db.Conversations.Add(created);
        db.TelegramChatMappings.Add(TelegramChatMapping.Create(chatId, created.Id));
        await db.SaveChangesAsync(ct);
        return created;
    }
}
