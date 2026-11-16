namespace Overmind.Infrastructure.Telegram;

public sealed record TelegramWaveCallbackData(WaveCallbackAction Action, Guid? ProjectId, string? Room)
{
    public const string Prefix = "wave";
    public const string ProjectToken = "proj";
    public const string RoomToken = "room";
    public const string CancelToken = "cancel";
    public const int CallbackDataLimit = 64;

    public static bool Owns(string? data) =>
        data is not null
        && data.Length > Prefix.Length
        && data.StartsWith(Prefix, StringComparison.Ordinal)
        && data[Prefix.Length] == ':';

    public static bool TryParse(string? data, out TelegramWaveCallbackData parsed)
    {
        parsed = new TelegramWaveCallbackData(WaveCallbackAction.Cancel, null, null);
        if (string.IsNullOrEmpty(data) || data.Length > CallbackDataLimit)
        {
            return false;
        }

        var segments = data.Split(':');
        if (segments.Length < 2 || segments[0] != Prefix)
        {
            return false;
        }

        if (segments[1] == CancelToken && segments.Length == 2)
        {
            parsed = new TelegramWaveCallbackData(WaveCallbackAction.Cancel, null, null);
            return true;
        }

        if (segments[1] == ProjectToken
            && segments.Length == 3
            && TryProjectId(segments[2], out var projectId))
        {
            parsed = new TelegramWaveCallbackData(WaveCallbackAction.Project, projectId, null);
            return true;
        }

        if (segments[1] == RoomToken
            && segments.Length == 4
            && TryProjectId(segments[2], out var roomProjectId)
            && IsRoom(segments[3]))
        {
            parsed = new TelegramWaveCallbackData(WaveCallbackAction.Room, roomProjectId, segments[3]);
            return true;
        }

        return false;
    }

    private static bool TryProjectId(string value, out Guid projectId)
    {
        projectId = Guid.Empty;
        return value.Length == 36 && Guid.TryParseExact(value, "D", out projectId);
    }

    private static bool IsRoom(string room) =>
        room.Length > 0 && !room.Contains(' ') && !room.Contains('/');
}
