using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramBotClientHandle(ITelegramBotClient client, HttpClient httpClient) : IDisposable
{
    public ITelegramBotClient Client { get; } = client;

    public void Dispose() => httpClient.Dispose();
}
