using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveCommandParserTests
{
    [Theory]
    [InlineData("/send_to_wave", TelegramWaveCommand.SendToWave)]
    [InlineData("/wave_snapshot", TelegramWaveCommand.WaveSnapshot)]
    [InlineData("/Send_To_Wave", TelegramWaveCommand.SendToWave)]
    [InlineData("/WAVE_SNAPSHOT", TelegramWaveCommand.WaveSnapshot)]
    [InlineData("  /wave_snapshot  ", TelegramWaveCommand.WaveSnapshot)]
    [InlineData("/send_to_wave ", TelegramWaveCommand.SendToWave)]
    public void TryMatch_RegisteredToken_ReturnsTheCommand(string text, TelegramWaveCommand expected)
    {
        TelegramWaveCommandParser.TryMatch(text, out var command).ShouldBeTrue();
        command.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wave_snapshot")]
    [InlineData("//wave_snapshot")]
    [InlineData("/wave_snapshot now")]
    [InlineData("/send_to_wave hello")]
    [InlineData("/wave_snapshot:42")]
    [InlineData("send /wave_snapshot")]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("hello there")]
    public void TryMatch_AnythingElse_FallsThrough(string? text) =>
        TelegramWaveCommandParser.TryMatch(text, out _).ShouldBeFalse();
}
