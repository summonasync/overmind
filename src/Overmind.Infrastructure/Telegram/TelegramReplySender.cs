using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramReplySender(
    TelegramBotClientCache clientCache,
    TelegramTransportOptionsProvider optionsProvider)
{
    public async Task<int> SendAsync(long chatId, IReadOnlyList<string> chunks, CancellationToken ct = default)
    {
        var transport = await optionsProvider.GetAsync(ct);
        if (!transport.IsRunnable)
        {
            throw new InvalidOperationException("Telegram transport is not runnable; replies cannot be delivered.");
        }

        var client = clientCache.GetOrCreate(transport).Client;
        var delivered = 0;
        try
        {
            foreach (var chunk in chunks)
            {
                await client.SendMessage(chatId, chunk, cancellationToken: ct);
                delivered++;
            }

            return delivered;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TelegramReplyDeliveryException(delivered, ex);
        }
    }
}
