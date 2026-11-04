namespace Overmind.Infrastructure.Telegram;

public sealed record TelegramCallback(
    long ChatId,
    string CallbackQueryId,
    int MessageId,
    string From,
    string? MessageText,
    string? Data);
