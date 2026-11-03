namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramBotClientCache(ITelegramBotClientFactory factory) : IDisposable
{
    private readonly object _lock = new();
    private TelegramBotClientHandle? _handle;
    private string? _token;

    public TelegramBotClientHandle GetOrCreate(TelegramTransportOptions options)
    {
        lock (_lock)
        {
            if (_handle is not null && _token == options.BotToken)
            {
                return _handle;
            }

            _handle?.Dispose();
            _handle = factory.Create(options);
            _token = options.BotToken;
            return _handle;
        }
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _handle?.Dispose();
            _handle = null;
            _token = null;
        }
    }

    public void Dispose() => Invalidate();
}
