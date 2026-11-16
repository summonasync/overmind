using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveCallbackDataTests
{
    private const string Project = "1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111";

    [Fact]
    public void TryParse_Cancel_CarriesNoPayload()
    {
        TelegramWaveCallbackData.TryParse("wave:cancel", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Cancel);
        data.ProjectId.ShouldBeNull();
        data.Room.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ProjectScheme_ReturnsTheProjectId()
    {
        TelegramWaveCallbackData.TryParse($"wave:proj:{Project}", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Project);
        data.ProjectId.ShouldBe(new Guid(Project));
        data.Room.ShouldBeNull();
    }

    [Fact]
    public void TryParse_RoomScheme_ReturnsProjectAndRoom()
    {
        TelegramWaveCallbackData.TryParse($"wave:room:{Project}:telegram-bridge", out var data).ShouldBeTrue();

        data.Action.ShouldBe(WaveCallbackAction.Room);
        data.ProjectId.ShouldBe(new Guid(Project));
        data.Room.ShouldBe("telegram-bridge");
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
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:two words")]
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:src/App")]
    [InlineData("wave:PROJ:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("wave:room:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:lane-name-over-the-data-limit")]
    public void TryParse_GarbageShapes_ReturnsFalse(string? data) =>
        TelegramWaveCallbackData.TryParse(data, out _).ShouldBeFalse();

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
}
