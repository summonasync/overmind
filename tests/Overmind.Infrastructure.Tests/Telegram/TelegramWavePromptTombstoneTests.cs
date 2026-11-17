using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWavePromptTombstoneTests
{
    private const long ChatId = 42;

    [Fact]
    public void Announce_ThenIsClosed_AnswersEveryPromptTheFlowIssued()
    {
        var tombstone = CreateTombstone();

        tombstone.Announce(ChatId, 21);
        tombstone.Announce(ChatId, 22);

        tombstone.IsClosed(ChatId, 21).ShouldBeTrue();
        tombstone.IsClosed(ChatId, 22).ShouldBeTrue();
        tombstone.IsClosed(ChatId, 23).ShouldBeFalse();
    }

    [Fact]
    public void IsClosed_IsScopedToTheChatThatReceivedThePrompt()
    {
        var tombstone = CreateTombstone();
        tombstone.Announce(ChatId, 21);

        tombstone.IsClosed(ChatId + 1, 21).ShouldBeFalse();
    }

    [Fact]
    public void NewInstance_KnowsNothingAboutThePromptsOfThePreviousProcess()
    {
        var previous = CreateTombstone();
        previous.Announce(ChatId, 21);

        CreateTombstone().IsClosed(ChatId, 21).ShouldBeFalse();
    }

    [Fact]
    public void Announce_BeyondTheCapacityEvictsTheOldestPrompt()
    {
        var tombstone = CreateTombstone();
        var first = 1_000L;
        for (var offset = 0; offset < TelegramWavePromptTombstone.MaxPromptIdsPerChat; offset++)
        {
            tombstone.Announce(ChatId, first + offset);
        }

        tombstone.Announce(ChatId, first + TelegramWavePromptTombstone.MaxPromptIdsPerChat);

        tombstone.IsClosed(ChatId, first).ShouldBeFalse();
        tombstone.IsClosed(ChatId, first + 1).ShouldBeTrue();
        tombstone.IsClosed(ChatId, first + TelegramWavePromptTombstone.MaxPromptIdsPerChat).ShouldBeTrue();
    }

    [Fact]
    public void Announce_AgainOverAPromptAlreadyRegistered_KeepsTheOriginalExpiry()
    {
        var clock = new TelegramTestClock();
        var tombstone = new TelegramWavePromptTombstone(clock);
        tombstone.Announce(ChatId, 21);
        clock.Advance(TelegramWavePromptTombstone.Retention - TimeSpan.FromMinutes(1));
        tombstone.Announce(ChatId, 21);
        clock.Advance(TimeSpan.FromMinutes(2));

        tombstone.IsClosed(ChatId, 21).ShouldBeFalse();
    }

    [Fact]
    public void OldPromptId_ExpiresWhileTheNewerPromptOfTheSameChatSurvives()
    {
        var clock = new TelegramTestClock();
        var tombstone = new TelegramWavePromptTombstone(clock);
        tombstone.Announce(ChatId, 21);
        clock.Advance(TelegramWavePromptTombstone.Retention - TimeSpan.FromMinutes(1));
        tombstone.Announce(ChatId, 22);
        clock.Advance(TimeSpan.FromMinutes(2));

        tombstone.IsClosed(ChatId, 21).ShouldBeFalse();
        tombstone.IsClosed(ChatId, 22).ShouldBeTrue();
    }

    [Fact]
    public void IsClosed_ExpiresThePromptIdWithoutWaitingForAnotherAnnounce()
    {
        var clock = new TelegramTestClock();
        var tombstone = new TelegramWavePromptTombstone(clock);
        tombstone.Announce(ChatId, 21);

        clock.Advance(TelegramWavePromptTombstone.Retention + TimeSpan.FromMinutes(1));

        tombstone.IsClosed(ChatId, 21).ShouldBeFalse();
    }

    [Fact]
    public void AnyAnnounce_DropsThePromptIdsOlderThanTheRetention()
    {
        var clock = new TelegramTestClock();
        var tombstone = new TelegramWavePromptTombstone(clock);
        tombstone.Announce(ChatId, 21);

        clock.Advance(TelegramWavePromptTombstone.Retention + TimeSpan.FromMinutes(1));
        tombstone.Announce(77, 30);

        tombstone.IsClosed(ChatId, 21).ShouldBeFalse();
        tombstone.IsClosed(77, 30).ShouldBeTrue();
    }

    [Fact]
    public void Announce_KeepsTheForeignLedgersThatAreStillFresh()
    {
        var tombstone = CreateTombstone();
        tombstone.Announce(ChatId, 21);
        tombstone.Announce(77, 22);

        tombstone.Announce(91, 23);

        tombstone.IsClosed(ChatId, 21).ShouldBeTrue();
        tombstone.IsClosed(77, 22).ShouldBeTrue();
    }

    [Fact]
    public void Announce_LeavesAnUnknownChatSilentlyClosed()
    {
        CreateTombstone().IsClosed(ChatId, 21).ShouldBeFalse();
    }

    private static TelegramWavePromptTombstone CreateTombstone() => new(new TelegramTestClock());
}
