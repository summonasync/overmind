namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramReplyDeliveryException(int deliveredChunks, Exception innerException)
    : Exception($"Telegram reply was partially delivered: {deliveredChunks} chunk(s) sent before the failure.", innerException)
{
    public int DeliveredChunks { get; } = deliveredChunks;
}
