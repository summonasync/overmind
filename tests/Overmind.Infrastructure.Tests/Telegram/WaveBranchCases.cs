using Overmind.Infrastructure.Telegram;

namespace Overmind.Infrastructure.Tests.Telegram;

internal static class WaveBranchCases
{
    public static readonly Guid PickerProjectId = Guid.Parse("1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111");
    public const string PickerDraftToken = "1f0aa0e0";
    public const string UnparsableRoomData = "wave:room:ZZZZZZZZ:1";

    public static string OversizeCancelData { get; } = $"wave:cancel:{new string('x', 70)}";

    public static IReadOnlyDictionary<WaveCallbackAction, string> CallbackData { get; } =
        new Dictionary<WaveCallbackAction, string>
        {
            [WaveCallbackAction.Cancel] = TelegramWaveCallbackData.CancelData,
            [WaveCallbackAction.Project] = $"wave:proj:{PickerProjectId:D}",
            [WaveCallbackAction.Room] = TelegramWaveCallbackData.RoomData(PickerDraftToken, 0),
            [WaveCallbackAction.SnapshotProject] = $"wave:sproj:{PickerProjectId:D}",
            [WaveCallbackAction.SnapshotCancel] = TelegramWaveCallbackData.SnapshotCancelData,
        };

    public static IReadOnlyDictionary<WaveCallbackAction, string> FlowAnswer { get; } =
        new Dictionary<WaveCallbackAction, string>
        {
            [WaveCallbackAction.Cancel] = TelegramWaveCommandService.CancelAnswer,
            [WaveCallbackAction.Project] = TelegramWaveCommandService.RoomPickAnswer,
            [WaveCallbackAction.Room] = TelegramWaveCommandService.PromptAnswer,
            [WaveCallbackAction.SnapshotProject] = TelegramWaveSnapshotService.SnapshotAnswer,
            [WaveCallbackAction.SnapshotCancel] = ITelegramWaveSnapshotService.DismissAnswer,
        };

    public enum WaveDoor
    {
        SendFlow,
        SnapshotDoor
    }

    public static IReadOnlyDictionary<TelegramWaveCommand, string> CommandText { get; } =
        new Dictionary<TelegramWaveCommand, string>
        {
            [TelegramWaveCommand.SendToWave] = TelegramWaveCommandParser.SendToWaveText,
            [TelegramWaveCommand.WaveSnapshot] = TelegramWaveCommandParser.WaveSnapshotText,
        };

    public static IReadOnlyDictionary<TelegramWaveCommand, WaveDoor> CommandDoor { get; } =
        new Dictionary<TelegramWaveCommand, WaveDoor>
        {
            [TelegramWaveCommand.SendToWave] = WaveDoor.SendFlow,
            [TelegramWaveCommand.WaveSnapshot] = WaveDoor.SnapshotDoor,
        };

    public static IEnumerable<object[]> WaveActions() =>
        [.. Enum.GetValues<WaveCallbackAction>().Select(action => new object[] { action })];

    public static IEnumerable<object[]> WaveCommands() =>
        [.. Enum.GetValues<TelegramWaveCommand>().Select(command => new object[] { command })];

    public static IEnumerable<object[]> EveryCommandSpelling() =>
        [
            .. Enum.GetValues<TelegramWaveCommand>().SelectMany(
                command => new object[][]
                {
                    [CommandText[command]],
                    [CommandText[command].ToUpperInvariant()],
                }),
        ];

    public static IEnumerable<object[]> UnparsableCallbackShapes() =>
        [
            [UnparsableRoomData],
            [OversizeCancelData],
        ];

    public static IEnumerable<object[]> EveryCallbackShape() =>
        [
            .. CallbackData.Values.Select(data => new object[] { data }),
            .. UnparsableCallbackShapes(),
        ];
}
