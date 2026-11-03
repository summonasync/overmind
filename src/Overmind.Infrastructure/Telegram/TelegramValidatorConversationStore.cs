using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramValidatorConversationStore(OvermindDbContext db)
{
    public const string ConversationTitle = "Telegram validator";

    public async Task<Conversation> GetOrCreateConversationAsync(
        long chatId, Guid? projectId, CancellationToken ct = default)
    {
        var mapping = await db.TelegramValidatorMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChatId == chatId && m.ProjectId == projectId, ct);
        if (mapping is not null)
        {
            var conversation = await db.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == mapping.ConversationId, ct);
            if (conversation is not null)
            {
                return conversation;
            }

            await db.TelegramValidatorMappings.Where(m => m.Id == mapping.Id).ExecuteDeleteAsync(ct);
        }

        var created = Conversation.Create(projectId, ConversationTitle);
        db.Conversations.Add(created);
        db.TelegramValidatorMappings.Add(TelegramValidatorMapping.Create(chatId, projectId, created.Id));
        await db.SaveChangesAsync(ct);
        return created;
    }
}
