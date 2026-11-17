namespace Overmind.Infrastructure.Telegram;

public interface ITelegramWaveSnapshotService
{
    public const string DismissAnswer = "Cancelled.";

    Task HandleCommandAsync(long chatId, CancellationToken ct);

    Task<string> PickProjectAsync(long chatId, Guid projectId, CancellationToken ct);
}
