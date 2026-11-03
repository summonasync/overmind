namespace Overmind.Infrastructure.Telegram;

public interface ITelegramBotClientFactory
{
    TelegramBotClientHandle Create(TelegramTransportOptions options);
}
