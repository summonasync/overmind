namespace Overmind.Infrastructure.Telegram;

public interface ITelegramWaveCommandService
{
    Task HandleAsync(TelegramWaveCommand command, TelegramIncomingMessage message, CancellationToken ct);

    Task<bool> TryHandleDraftReplyAsync(TelegramIncomingMessage message, CancellationToken ct);

    Task<string> PickProjectAsync(long chatId, Guid projectId, CancellationToken ct);

    Task<string> PickRoomAsync(long chatId, string draftToken, int roomIndex, CancellationToken ct);

    string Cancel(long chatId);
}
