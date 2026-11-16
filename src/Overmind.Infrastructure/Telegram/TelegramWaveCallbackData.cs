using System.Globalization;
using System.Text;

namespace Overmind.Infrastructure.Telegram;

// The scheme tokens are matched Ordinal because Telegram hands callback_data back byte for
// byte, while the message door (TelegramWaveCommandParser) matches command tokens
// case-insensitively. The asymmetry is deliberate and mirrors the notification door of feature
// 213: do not unify the two, a user types commands by hand but never types callback data.
public sealed record TelegramWaveCallbackData(
    WaveCallbackAction Action,
    Guid? ProjectId = null,
    string? DraftToken = null,
    int? RoomIndex = null)
{
    public const string Prefix = "wave";
    public const string ProjectToken = "proj";
    public const string RoomToken = "room";
    public const string CancelToken = "cancel";
    public const string CancelData = Prefix + ":" + CancelToken;
    public const int CallbackDataLimit = 64;
    public const int DraftTokenLength = 8;
    public const int MaxRoomIndex = 999;

    public static string ProjectData(Guid projectId) =>
        WithinBudget($"{Prefix}:{ProjectToken}:{projectId:D}");

    // A lane name is up to 64 characters and the project guid alone eats 46 of the 64 allowed
    // bytes, so a room is addressed by its offset into the draft's own room snapshot instead.
    public static string RoomData(string draftToken, int roomIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(roomIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roomIndex, MaxRoomIndex);
        ArgumentOutOfRangeException.ThrowIfNotEqual(draftToken.Length, DraftTokenLength);

        return WithinBudget(
            $"{Prefix}:{RoomToken}:{draftToken}:{roomIndex.ToString(CultureInfo.InvariantCulture)}");
    }

    public static int ByteLength(string data) => Encoding.UTF8.GetByteCount(data);

    public static bool Owns(string? data) =>
        data is not null
        && data.Length > Prefix.Length
        && data.StartsWith(Prefix, StringComparison.Ordinal)
        && data[Prefix.Length] == ':';

    public static bool TryParse(string? data, out TelegramWaveCallbackData parsed)
    {
        parsed = new TelegramWaveCallbackData(WaveCallbackAction.Cancel);
        if (string.IsNullOrEmpty(data) || ByteLength(data) > CallbackDataLimit)
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
            return true;
        }

        if (segments[1] == ProjectToken
            && segments.Length == 3
            && TryProjectId(segments[2], out var projectId))
        {
            parsed = new TelegramWaveCallbackData(WaveCallbackAction.Project, projectId);
            return true;
        }

        if (segments[1] == RoomToken
            && segments.Length == 4
            && IsDraftToken(segments[2])
            && TryRoomIndex(segments[3], out var roomIndex))
        {
            parsed = new TelegramWaveCallbackData(WaveCallbackAction.Room, null, segments[2], roomIndex);
            return true;
        }

        return false;
    }

    private static string WithinBudget(string data) =>
        ByteLength(data) <= CallbackDataLimit
            ? data
            : throw new InvalidOperationException(
                $"Callback data '{data}' is {ByteLength(data)} bytes; the Telegram bound is {CallbackDataLimit}.");

    private static bool TryProjectId(string value, out Guid projectId)
    {
        projectId = Guid.Empty;
        return value.Length == 36 && Guid.TryParseExact(value, "D", out projectId);
    }

    private static bool IsDraftToken(string value)
    {
        if (value.Length != DraftTokenLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryRoomIndex(string value, out int roomIndex) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out roomIndex)
        && roomIndex <= MaxRoomIndex;
}
