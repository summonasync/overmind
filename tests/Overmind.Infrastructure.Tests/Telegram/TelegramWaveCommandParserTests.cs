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
    [MemberData(nameof(WaveBranchCases.WaveCommands), MemberType = typeof(WaveBranchCases))]
    public void TryMatch_CanonicalSpellingOfEveryCommand_RoutesToThatCommand(TelegramWaveCommand command)
    {
        var text = WaveBranchCases.CommandText[command];

        TelegramWaveCommandParser.TryMatch(text, out var matched).ShouldBeTrue();
        matched.ShouldBe(command);
    }

    [Fact]
    public void CommandBranchMaps_CoverEveryWaveCommand()
    {
        var commands = Enum.GetValues<TelegramWaveCommand>();

        WaveBranchCases.CommandText.Count.ShouldBe(commands.Length);
        WaveBranchCases.CommandDoor.Count.ShouldBe(commands.Length);
        foreach (var command in commands)
        {
            WaveBranchCases.CommandText.ContainsKey(command)
                .ShouldBeTrue($"{command} has no canonical spelling to drive the message door");
            WaveBranchCases.CommandDoor.ContainsKey(command)
                .ShouldBeTrue($"{command} has no promised door to be checked against");
        }
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
