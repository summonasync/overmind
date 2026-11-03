namespace Overmind.Infrastructure.Telegram;

public sealed record TelegramTransportOptions
{
    public required bool Enabled { get; init; }
    public required string? BotToken { get; init; }
    public required IReadOnlyList<long> AllowedChatIds { get; init; }

    public bool IsRunnable => Enabled && !string.IsNullOrWhiteSpace(BotToken);
}
