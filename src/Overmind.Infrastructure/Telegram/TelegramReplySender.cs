using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramReplySender(
    TelegramBotClientCache clientCache,
    TelegramTransportOptionsProvider optionsProvider)
{
    public async Task<IReadOnlyList<long>> SendAsync(
        long chatId,
        IReadOnlyList<string> chunks,
        InlineKeyboardMarkup? replyMarkup = null,
        IReadOnlyList<MessageEntity>? entities = null,
        CancellationToken ct = default)
    {
        var transport = await optionsProvider.GetAsync(ct);
        if (!transport.IsRunnable)
        {
            throw new InvalidOperationException("Telegram transport is not runnable; replies cannot be delivered.");
        }

        var client = clientCache.GetOrCreate(transport).Client;
        var delivered = new List<long>();
        try
        {
            foreach (var chunk in chunks)
            {
                var message = await client.SendMessage(
                    chatId, chunk, entities: entities, replyMarkup: replyMarkup, cancellationToken: ct);
                delivered.Add(message.Id);
            }

            return delivered;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TelegramReplyDeliveryException(delivered.Count, ex);
        }
    }
}
