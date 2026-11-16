using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public static class TelegramWaveKeyboardMarkup
{
    // Telegram's own ceiling for inline button text, in characters: a different bound from the
    // 64 byte callback_data budget, even though both happen to be sixty-four.
    public const int MaxLabelLength = 64;

    private const int MaxButtonsPerRow = 2;

    public static InlineKeyboardMarkup Projects(IReadOnlyList<TelegramWaveProjectChoice> choices)
    {
        var rows = Chunked([
            .. choices.Select(choice => InlineKeyboardButton.WithCallbackData(
                Label(choice.Name),
                TelegramWaveCallbackData.ProjectData(choice.Id)))
        ]);
        rows.Add([Cancel()]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Rooms(string draftToken, IReadOnlyList<string> rooms)
    {
        var rows = Chunked([
            .. rooms.Select((room, index) => InlineKeyboardButton.WithCallbackData(
                Label(room),
                TelegramWaveCallbackData.RoomData(draftToken, index)))
        ]);
        rows.Add([Cancel()]);
        return new InlineKeyboardMarkup(rows);
    }

    public static ForceReplyMarkup Prompt() =>
        new() { InputFieldPlaceholder = "the signal for the room" };

    private static InlineKeyboardButton Cancel() =>
        InlineKeyboardButton.WithCallbackData("✖️ Cancel", TelegramWaveCallbackData.CancelData);

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
