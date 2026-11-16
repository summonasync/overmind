namespace Overmind.Infrastructure.Telegram;

public static class TelegramWaveCommandParser
{
    public const string SendToWaveText = "/send_to_wave";
    public const string WaveSnapshotText = "/wave_snapshot";

    public static bool TryMatch(string? text, out TelegramWaveCommand command)
    {
        command = default;
        if (text is null)
        {
            return false;
        }

        var token = text.Trim();
        if (token.Equals(SendToWaveText, StringComparison.OrdinalIgnoreCase))
        {
            command = TelegramWaveCommand.SendToWave;
            return true;
        }

        if (token.Equals(WaveSnapshotText, StringComparison.OrdinalIgnoreCase))
        {
            command = TelegramWaveCommand.WaveSnapshot;
            return true;
        }

        return false;
    }
}
