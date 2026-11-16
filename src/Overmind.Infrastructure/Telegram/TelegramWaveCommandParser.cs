namespace Overmind.Infrastructure.Telegram;

public static class TelegramWaveCommandParser
{
    public const string SendToWaveText = "/send_to_wave";
    public const string WaveSnapshotText = "/wave_snapshot";

    // Case-insensitive on purpose: a human types these. The callback door
    // (TelegramWaveCallbackData) matches its scheme tokens Ordinal, mirroring the 213 notification
    // door. The asymmetry is deliberate: keep it, do not unify the two.
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
