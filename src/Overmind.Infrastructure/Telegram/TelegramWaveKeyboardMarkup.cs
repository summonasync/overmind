using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public static class TelegramWaveKeyboardMarkup
{
    // Telegram's own ceiling for inline button text, in characters: a different bound from the
    // 64 byte callback_data budget, even though both happen to be sixty-four.
    public const int MaxLabelLength = 64;

    private const int MaxButtonsPerRow = 2;

    public static InlineKeyboardMarkup Projects(IReadOnlyList<TelegramWaveProjectChoice> choices) =>
        ProjectPicker(choices, TelegramWaveCallbackData.ProjectData, TelegramWaveCallbackData.CancelData);

    public static InlineKeyboardMarkup SnapshotProjects(IReadOnlyList<TelegramWaveProjectChoice> choices) =>
        ProjectPicker(choices, TelegramWaveCallbackData.SnapshotProjectData, TelegramWaveCallbackData.SnapshotCancelData);

    public static InlineKeyboardMarkup Rooms(string draftToken, IReadOnlyList<string> rooms)
    {
        var rows = Chunked([
            .. rooms.Select((room, index) => InlineKeyboardButton.WithCallbackData(
                Label(room),
                TelegramWaveCallbackData.RoomData(draftToken, index)))
        ]);
        rows.Add([Cancel(TelegramWaveCallbackData.CancelData)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static ForceReplyMarkup Prompt() =>
        new() { InputFieldPlaceholder = "the signal for the room" };

    private static InlineKeyboardMarkup ProjectPicker(
        IReadOnlyList<TelegramWaveProjectChoice> choices,
        Func<Guid, string> dataFor,
        string cancelData)
    {
        var rows = Chunked([
            .. choices.Select(choice => InlineKeyboardButton.WithCallbackData(
                Label(choice.Name),
                dataFor(choice.Id)))
        ]);
        rows.Add([Cancel(cancelData)]);
        return new InlineKeyboardMarkup(rows);
    }

    private static InlineKeyboardButton Cancel(string data) =>
        InlineKeyboardButton.WithCallbackData("✖️ Cancel", data);

    private static List<List<InlineKeyboardButton>> Chunked(IReadOnlyList<InlineKeyboardButton> buttons)
    {
        var rows = new List<List<InlineKeyboardButton>>();
        for (var offset = 0; offset < buttons.Count; offset += MaxButtonsPerRow)
        {
            rows.Add([.. buttons.Skip(offset).Take(MaxButtonsPerRow)]);
        }

        return rows;
    }

    private static string Label(string text) =>
        text.Length <= MaxLabelLength
            ? text
            : string.Concat(text.AsSpan(0, MaxLabelLength - 1), "…");
}
