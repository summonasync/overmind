namespace Overmind.Infrastructure.Telegram;

public sealed record TelegramIncomingMessage(
    long ChatId,
    string Text,
    long? RepliedToMessageId = null,
    string? From = null);
