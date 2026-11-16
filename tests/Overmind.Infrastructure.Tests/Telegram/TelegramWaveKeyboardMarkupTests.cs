using System.Text;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveKeyboardMarkupTests
{
    private const string DraftToken = "1f0aa0e0";
    private const int LaneNameCeiling = 64;
    private static readonly Guid Project = Guid.Parse("1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111");

    [Fact]
    public void Projects_CarriesOneButtonPerProjectAndTheCancelDoor()
    {
        var second = Guid.NewGuid();
        var markup = TelegramWaveKeyboardMarkup.Projects([
            new TelegramWaveProjectChoice(Project, "Overmind"),
            new TelegramWaveProjectChoice(second, "Grimoire")
        ]);

        Labels(markup).ShouldBe(["Overmind", "Grimoire", "✖️ Cancel"]);
        Data(markup).ShouldBe([
            $"wave:proj:{Project:D}",
            $"wave:proj:{second:D}",
            TelegramWaveCallbackData.CancelData
        ]);
    }

    [Fact]
    public void SnapshotProjects_RidesItsOwnStatelessSchemeSoOnePressCannotMeanTwoFlows()
    {
        var markup = TelegramWaveKeyboardMarkup.SnapshotProjects([
            new TelegramWaveProjectChoice(Project, "Overmind")
        ]);

        Labels(markup).ShouldBe(["Overmind", "✖️ Cancel"]);
        Data(markup).ShouldBe([
            $"wave:sproj:{Project:D}",
            TelegramWaveCallbackData.SnapshotCancelData
        ]);
        foreach (var entry in Data(markup))
        {
            Encoding.UTF8.GetByteCount(entry).ShouldBeLessThanOrEqualTo(
                TelegramWaveCallbackData.CallbackDataLimit);
        }
    }

    [Fact]
    public void Rooms_AddressesEveryRoomByIndexInsideTheByteBudget()
    {
        var rooms = new List<string>
        {
            "all",
            "mcp-tool-description-diet",
            "мост-для-волны-через-телеграм-мост",
            new string('ы', LaneNameCeiling)
        };

        var markup = TelegramWaveKeyboardMarkup.Rooms(DraftToken, rooms);

        Labels(markup).Take(4).ShouldBe(rooms);
        var data = Data(markup).Where(entry =>
            entry.StartsWith("wave:room:", StringComparison.Ordinal)).ToList();
        data.Count.ShouldBe(4);
        foreach (var entry in data)
        {
            Encoding.UTF8.GetByteCount(entry).ShouldBeLessThanOrEqualTo(
                TelegramWaveCallbackData.CallbackDataLimit);
        }

        data.Select(entry => entry.Split(':')[3]).ShouldBe(["0", "1", "2", "3"]);
        Buttons(markup).Count(button =>
            button.CallbackData == TelegramWaveCallbackData.CancelData).ShouldBe(1);
    }

    [Fact]
    public void Rooms_LongestAddressableLaneNameDoesNotFitOnceTheProjectGuidIsCarried()
    {
        var longest = new string('m', LaneNameCeiling);

        Encoding.UTF8.GetByteCount($"wave:room:{Project:D}:{longest}")
            .ShouldBeGreaterThan(TelegramWaveCallbackData.CallbackDataLimit);
    }

    [Fact]
    public void Rooms_CyrillicNameOfTheSameLengthBreaksACharacterCountCheck()
    {
        var cyrillic = new string('ы', 30);

        cyrillic.Length.ShouldBeLessThan(TelegramWaveCallbackData.CallbackDataLimit);
        Encoding.UTF8.GetByteCount($"wave:room:{Project:D}:{cyrillic}")
            .ShouldBeGreaterThan(TelegramWaveCallbackData.CallbackDataLimit);
    }

    [Fact]
    public void Projects_TruncatesTheLabelButNeverTheCallbackData()
    {
        var markup = TelegramWaveKeyboardMarkup.Projects([
            new TelegramWaveProjectChoice(Project, new string('ы', 90))
        ]);

        Labels(markup)[0].Length.ShouldBe(TelegramWaveKeyboardMarkup.MaxLabelLength);
        Labels(markup)[0].ShouldEndWith("…");
        Data(markup)[0].ShouldBe($"wave:proj:{Project:D}");
    }

    [Fact]
    public void Prompt_IsAForceReplyWithoutInlineKeyboard()
    {
        var markup = TelegramWaveKeyboardMarkup.Prompt();

        markup.ForceReply.ShouldBeTrue();
        markup.InputFieldPlaceholder.ShouldNotBeNullOrWhiteSpace();
        Encoding.UTF8.GetByteCount(markup.InputFieldPlaceholder!)
            .ShouldBeLessThanOrEqualTo(TelegramWaveCallbackData.CallbackDataLimit);
    }

    private static List<InlineKeyboardButton> Buttons(InlineKeyboardMarkup markup) =>
        [.. markup.InlineKeyboard.SelectMany(row => row)];

    private static List<string> Data(InlineKeyboardMarkup markup) =>
        [.. Buttons(markup).Select(button => button.CallbackData ?? string.Empty)];

    private static List<string> Labels(InlineKeyboardMarkup markup) =>
        [.. Buttons(markup).Select(button => button.Text)];
}
