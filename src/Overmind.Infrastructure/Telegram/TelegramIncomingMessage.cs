namespace Overmind.Infrastructure.Telegram;

public sealed record TelegramIncomingMessage(long ChatId, string Text);
