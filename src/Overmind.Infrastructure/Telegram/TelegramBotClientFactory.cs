using Overmind.Infrastructure.Http;
using Telegram.Bot;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramBotClientFactory : ITelegramBotClientFactory
{
    internal const string DefaultApiBaseUrl = "https://api.telegram.org";

    public TelegramBotClientHandle Create(TelegramTransportOptions options)
    {
        var handler = LoopbackHttp.CreateHandler(DefaultApiBaseUrl);
        var httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new TelegramBotClient(new TelegramBotClientOptions(options.BotToken!), httpClient);
        return new TelegramBotClientHandle(client, httpClient);
    }
}
