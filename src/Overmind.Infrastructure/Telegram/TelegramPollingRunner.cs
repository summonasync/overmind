using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramPollingRunner(
    IServiceScopeFactory scopeFactory,
    TelegramBotClientCache clientCache,
    ILogger<TelegramPollingRunner> logger)
{
    internal static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);
    internal const int LongPollSeconds = 50;

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
                clientCache.Invalidate();
                await WaitQuietlyAsync(ErrorDelay, ct);
            }
        }

        clientCache.Invalidate();
    }

    private async Task PollCycleAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<TelegramTransportOptionsProvider>();
        var options = await provider.GetAsync(ct);
        if (!options.IsRunnable)
        {
            clientCache.Invalidate();
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
        var callbackHandler = scope.ServiceProvider.GetService<ITelegramCallbackHandler>();

        var updates = await clientCache.GetOrCreate(options)
            .Client.GetUpdates(
                offset: _offset == 0 ? null : _offset,
                timeout: LongPollSeconds,
                allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery],
                cancellationToken: ct);
        foreach (var update in updates)
        {
            _offset = update.Id + 1;
            if (update.CallbackQuery is { } query)
            {
                await DispatchCallbackAsync(callbackHandler, query, ct);
                continue;
            }

            await ProcessUpdateAsync(handler, update, options, ct);
        }
    }

    private async Task DispatchCallbackAsync(ITelegramCallbackHandler? handler, CallbackQuery query, CancellationToken ct)
    {
        var message = query.Message;
        if (message is null)
        {
            logger.LogDebug("Telegram callback {CallbackQueryId} carries no message; ignored", query.Id);
            return;
        }

        if (handler is null)
        {
            logger.LogDebug(
                "Telegram callback {CallbackQueryId} from chat {ChatId} arrived while no callback handler is wired; ignored",
                query.Id, message.Chat.Id);
            return;
        }

        try
        {
            await handler.HandleAsync(ToCallback(query, message), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram callback from chat {ChatId} failed dispatch", message.Chat.Id);
        }
    }

    private static TelegramCallback ToCallback(CallbackQuery query, Message message)
    {
        var from = DisplayName(query.From);
        return new TelegramCallback(message.Chat.Id, query.Id, message.Id, from, message.Text, query.Data);
    }

    private static string DisplayName(User from) =>
        from.Username is { } username ? $"@{username}" : from.FirstName;

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
            logger.LogInformation(
                "Telegram message from chat {ChatId} is not allowlisted; ignored", message.Chat.Id);
            return;
        }

        try
        {
            await handler.HandleAsync(
                new TelegramIncomingMessage(
                    message.Chat.Id,
                    message.Text,
                    message.ReplyToMessage?.MessageId,
                    message.From is { } from ? DisplayName(from) : null,
                    message.Id,
                    update.Id), ct);
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
