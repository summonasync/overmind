using System.Text;
using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveCallbackDataTests
{
    private const string Project = "1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111";
    private const string DraftToken = "1f0aa0e0";

    [Fact]
    public void TryParse_Cancel_CarriesNoPayload()
    {
        TelegramWaveCallbackData.TryParse("wave:cancel", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Cancel);
        data.ProjectId.ShouldBeNull();
        data.DraftToken.ShouldBeNull();
        data.RoomIndex.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ProjectScheme_ReturnsTheProjectId()
    {
        TelegramWaveCallbackData.TryParse($"wave:proj:{Project}", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Project);
        data.ProjectId.ShouldBe(new Guid(Project));
        data.DraftToken.ShouldBeNull();
    }

    [Fact]
    public void TryParse_RoomScheme_ReturnsDraftTokenAndRoomIndex()
    {
        TelegramWaveCallbackData.TryParse($"wave:room:{DraftToken}:3", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Room);
        data.DraftToken.ShouldBe(DraftToken);
        data.RoomIndex.ShouldBe(3);
    }

    [Fact]
    public void TryParse_RoomSchemeWithLaneName_IsRejectedBecauseNamesDoNotFitTheBudget()
    {
        var data = $"wave:room:{Project}:mcp-tool-description-diet";

        TelegramWaveCallbackData.TryParse(data, out _).ShouldBeFalse();
        Encoding.UTF8.GetByteCount(data).ShouldBeGreaterThan(TelegramWaveCallbackData.CallbackDataLimit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wave")]
    [InlineData("wave:")]
    [InlineData("wave:cancel:extra")]
    [InlineData("wave:unknown")]
    [InlineData("waves:cancel")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve")]
    [InlineData(" wave:cancel")]
    [InlineData("wave:cancel ")]
    [InlineData("wave:proj")]
    [InlineData("wave:proj:1f0aa0e0")]
    [InlineData("wave:proj:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:extra")]
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:")]
    [InlineData("wave:room:1f0aa0e0:two words")]
    [InlineData("wave:room:1f0aa0e0:src/App")]
    [InlineData("wave:room:1F0AA0E0:1")]
    [InlineData("wave:room:1f0aa0e0:1:extra")]
    [InlineData("wave:room:1f0aa0e0:-1")]
    [InlineData("wave:room:1f0aa0e0:1000")]
    [InlineData("wave:PROJ:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("wave:room:1f0aa0e0:lane-name-over-the-data-limit")]
    public void TryParse_GarbageShapes_ReturnsFalse(string? data) =>
        TelegramWaveCallbackData.TryParse(data, out _).ShouldBeFalse();

    [Fact]
    public void TryParse_DataOverTheByteBudget_IsRejectedEvenWhenTheCharacterCountFits()
    {
        var roomName = new string('ы', TelegramWaveCallbackData.CallbackDataLimit - 10 - 8 - 1);
        var smuggled = $"wave:room:{DraftToken}:{roomName}";

        smuggled.Length.ShouldBeLessThanOrEqualTo(TelegramWaveCallbackData.CallbackDataLimit);
        Encoding.UTF8.GetByteCount(smuggled).ShouldBeGreaterThan(TelegramWaveCallbackData.CallbackDataLimit);
        TelegramWaveCallbackData.TryParse(smuggled, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("wave:cancel", true)]
    [InlineData("wave:", true)]
    [InlineData("wave", false)]
    [InlineData("waves:cancel", false)]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve", false)]
    [InlineData("garbage", false)]
    [InlineData(null, false)]
    public void Owns_FirstSegmentWave_RoutesToTheWaveDoor(string? data, bool expected) =>
        TelegramWaveCallbackData.Owns(data).ShouldBe(expected);

    [Fact]
    public void ProjectData_IsAGuidWidePayloadInsideTheByteBudget()
    {
        var data = TelegramWaveCallbackData.ProjectData(new Guid(Project));

        data.ShouldBe($"wave:proj:{Project}");
        TelegramWaveCallbackData.ByteLength(data).ShouldBe(46);
        TelegramWaveCallbackData.ByteLength(data)
            .ShouldBeLessThanOrEqualTo(TelegramWaveCallbackData.CallbackDataLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(64)]
    [InlineData(999)]
    public void RoomData_StaysInsideTheByteBudgetForEveryAddressableRoomIndex(int roomIndex)
    {
        var data = TelegramWaveCallbackData.RoomData(DraftToken, roomIndex);

        TelegramWaveCallbackData.ByteLength(data)
            .ShouldBeLessThanOrEqualTo(TelegramWaveCallbackData.CallbackDataLimit);
        TelegramWaveCallbackData.TryParse(data, out var parsed).ShouldBeTrue();
        parsed.RoomIndex.ShouldBe(roomIndex);
        parsed.DraftToken.ShouldBe(DraftToken);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void RoomData_RefusesAnIndexItCannotParseBack(int roomIndex) =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => TelegramWaveCallbackData.RoomData(DraftToken, roomIndex));

    [Fact]
    public void RoomData_RefusesATokenItCannotParseBack() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => TelegramWaveCallbackData.RoomData("toolongtoken", 0));

    [Fact]
    public void MeasuredCeiling_TheGuidSchemeCouldOnlyAddressShortRooms()
    {
        var carried = Encoding.UTF8.GetByteCount($"wave:room:{Project}:");
        var roomBudget = TelegramWaveCallbackData.CallbackDataLimit - carried;

        roomBudget.ShouldBe(17);
        "mcp-tool-description-diet".Length.ShouldBeGreaterThan(roomBudget);
    }
}
