using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Telegram;
using Telegram.Bot;

namespace Overmind.Infrastructure.Tests.Telegram;

internal sealed class StubTelegramBotClientFactory(FakeTelegramApi api) : ITelegramBotClientFactory
{
    public int CreatedCount { get; private set; }

    public TelegramBotClientHandle Create(TelegramTransportOptions options)
    {
        CreatedCount++;
        var httpClient = new HttpClient(api) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new TelegramBotClient(
            new TelegramBotClientOptions(options.BotToken!, "http://localhost/fake/"), httpClient);
        return new TelegramBotClientHandle(client, httpClient);
    }
}

internal sealed class CollectingLogger<TCategory>(List<string> sink) : ILogger<TCategory>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (sink)
        {
            sink.Add($"{logLevel}: {formatter(state, exception)} :: {exception?.GetType().Name}");
        }
    }
}

internal sealed class TelegramTestClock : TimeProvider
{
    private TimeSpan _offset;

    public void Advance(TimeSpan delta) => _offset += delta;

    public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow().Add(_offset);
}
