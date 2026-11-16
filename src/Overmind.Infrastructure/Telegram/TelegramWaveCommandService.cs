using Microsoft.Extensions.Logging;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveCommandService(
    TelegramReplySender sender,
    ILogger<TelegramWaveCommandService> logger) : ITelegramWaveCommandService
{
    public const string NotWiredYetReply =
        "Command recognized, but the wave flow behind it is not wired yet.";

    public async Task HandleAsync(TelegramWaveCommand command, TelegramIncomingMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Telegram wave command {Command} from chat {ChatId} arrived before its flow was wired",
            command, message.ChatId);

        try
        {
            await sender.SendAsync(
                message.ChatId, TelegramReplySplitter.Split(NotWiredYetReply), ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Telegram wave command notice to chat {ChatId} could not be delivered",
                message.ChatId);
        }
    }
}
