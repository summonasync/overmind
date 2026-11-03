using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramPollingRunner(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClientFactory botClientFactory,
    ILogger<TelegramPollingRunner> logger)
{
    internal static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);
    internal const int LongPollSeconds = 50;

    private TelegramBotClientHandle? _client;
    private string? _clientToken;
    private int _offset;
    private bool _missingHandlerLogged;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollCycleAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telegram polling cycle failed; retrying shortly");
                DropClient();
                await WaitQuietlyAsync(ErrorDelay, ct);
            }
        }

        DropClient();
    }

    private async Task PollCycleAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<TelegramTransportOptionsProvider>();
        var options = await provider.GetAsync(ct);
        if (!options.IsRunnable)
        {
            DropClient();
            await WaitQuietlyAsync(IdleDelay, ct);
            return;
        }

        var handler = scope.ServiceProvider.GetService<ITelegramMessageHandler>();
        if (handler is null)
        {
            if (!_missingHandlerLogged)
            {
                _missingHandlerLogged = true;
                logger.LogWarning(
                    "Telegram transport is enabled but no message handler is wired; incoming messages stay unconfirmed on the bot API");
            }

            await WaitQuietlyAsync(IdleDelay, ct);
            return;
        }

        _missingHandlerLogged = false;

        var updates = await EnsureClient(options)
            .Client.GetUpdates(
                offset: _offset == 0 ? null : _offset,
                timeout: LongPollSeconds,
                allowedUpdates: [UpdateType.Message],
                cancellationToken: ct);
        foreach (var update in updates)
        {
            _offset = update.Id + 1;
            await ProcessUpdateAsync(handler, update, options, ct);
        }
    }

    internal async Task ProcessUpdateAsync(
        ITelegramMessageHandler handler, Update update, TelegramTransportOptions options, CancellationToken ct)
    {
        var message = update.Message;
        if (string.IsNullOrEmpty(message?.Text))
        {
            logger.LogDebug("Telegram update {UpdateId} carries no text; ignored", update.Id);
            return;
        }

        if (!options.AllowedChatIds.Contains(message.Chat.Id))
        {
            logger.LogDebug("Telegram message from chat {ChatId} is not allowlisted; ignored", message.Chat.Id);
            return;
        }

        try
        {
            await handler.HandleAsync(new TelegramIncomingMessage(message.Chat.Id, message.Text), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram message from chat {ChatId} failed handling", message.Chat.Id);
        }
    }

    private TelegramBotClientHandle EnsureClient(TelegramTransportOptions options)
    {
        if (_client is not null && _clientToken == options.BotToken)
        {
            return _client;
        }

        DropClient();
        _client = botClientFactory.Create(options);
        _clientToken = options.BotToken;
        return _client;
    }

    private void DropClient()
    {
        _client?.Dispose();
        _client = null;
        _clientToken = null;
    }

    private async Task WaitQuietlyAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
