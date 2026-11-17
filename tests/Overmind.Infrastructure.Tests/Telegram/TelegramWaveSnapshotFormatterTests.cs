using Overmind.Core;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;
using Formatter = Overmind.Infrastructure.Telegram.TelegramWaveSnapshotFormatter;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class TelegramWaveSnapshotFormatterTests
{
    private static readonly DateTime Generated = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ClaimStates_RenderDistinctly()
    {
        var holders = new List<Formatter.Holder>
        {
            new("215-a", "tucker", "qwen", AgentSessionLiveness.Alive, null, null),
            new("215-b", "milk-toast", "qwen", AgentSessionLiveness.Stale, null, null),
            new("215-c", "boss", "claude", AgentSessionLiveness.Ended, null, null),
            new("215-d", "ghost", "claude", AgentSessionLiveness.Released, null, null),
        };

        var text = Format(holders: holders);

        text.ShouldContain("215-a · tucker/qwen [alive]");
        text.ShouldContain("215-b · milk-toast/qwen [stale]");
        text.ShouldContain("215-c · boss/claude [ended]");
        text.ShouldContain("215-d · ghost/claude [released]");
    }

    [Fact]
    public void Holder_CarriesItsLaneAndTheBoundAliasOfThatLane()
    {
        var text = Format(holders:
        [
            new Formatter.Holder(
                "215-wave", "tucker", "qwen", AgentSessionLiveness.Alive, "telegram-bridge", "milk-toast")
        ]);

        text.ShouldContain(
            "215-wave · tucker/qwen [alive] lane telegram-bridge (bound milk-toast)");
    }

    [Fact]
    public void Holder_WithoutAVendorOrLane_RendersNeitherSeparator()
    {
        var text = Format(holders:
        [
            new Formatter.Holder("215-wave", "tucker", null, AgentSessionLiveness.Stale, null, null)
        ]);

        text.ShouldContain("215-wave · tucker [stale]");
        text.ShouldNotContain("tucker/");
        text.ShouldNotContain("lane ");
    }

    [Fact]
    public void QueueHead_BeyondTheCap_TruncatesWithTheHiddenCount()
    {
        var queue = new List<Formatter.QueueRow>();
        for (var index = 1; index <= 7; index++)
        {
            queue.Add(new Formatter.QueueRow($"f{index}", FeaturePriority.P2, PlanningFeatureStatus.Ready));
        }

        var text = Format(queue: queue);

        text.ShouldContain("1. p2 f1 [ready]");
        text.ShouldContain($"5. p2 f{Formatter.MaxQueueRows} [ready]");
        text.ShouldContain($"+{7 - Formatter.MaxQueueRows} more in the queue");
        text.ShouldNotContain("f6 [ready]");
        text.ShouldNotContain("f7 [ready]");
    }

    [Fact]
    public void QueueHead_RendersPriorityAndSpacedStatus()
    {
        var text = Format(queue:
        [
            new Formatter.QueueRow("215-a", FeaturePriority.P1, PlanningFeatureStatus.InProgress),
            new Formatter.QueueRow("215-b", FeaturePriority.P0, PlanningFeatureStatus.Review),
        ]);

        text.ShouldContain("1. p1 215-a [in progress]");
        text.ShouldContain("2. p0 215-b [review]");
    }

    [Fact]
    public void Rooms_RendersCountsAndRelativeAges()
    {
        var text = Format(rooms:
        [
            new Formatter.RoomTraffic("all", 3, Generated - TimeSpan.FromMinutes(12)),
            new Formatter.RoomTraffic("telegram-bridge", 1, Generated - TimeSpan.FromHours(5)),
            new Formatter.RoomTraffic("frontend", 1, null),
        ]);

        text.ShouldContain("all · 3 mentions · last 12m");
        text.ShouldContain("telegram-bridge · 1 mentions · last 5h");
        text.ShouldContain("frontend · 1 mentions · last never");
    }

    [Theory]
    [InlineData(0, "now")]
    [InlineData(45, "45m")]
    [InlineData(75, "1h")]
    [InlineData(25 * 60, "1d")]
    [InlineData(72 * 60, "3d")]
    public void RoomAge_IsMeasuredAgainstTheSnapshotMoment(int ageMinutes, string rendered)
    {
        var text = Format(rooms:
        [
            new Formatter.RoomTraffic("all", 1, Generated - TimeSpan.FromMinutes(ageMinutes))
        ]);

        text.ShouldContain($"last {rendered}");
    }

    [Fact]
    public void Rooms_BeyondTheCap_TruncatesWithTheHiddenRoomCount()
    {
        var rooms = new List<Formatter.RoomTraffic>();
        for (var index = 0; index < 10; index++)
        {
            rooms.Add(new Formatter.RoomTraffic($"lane-{index}", 1, Generated));
        }

        var text = Format(rooms: rooms);

        text.ShouldContain($"+{10 - Formatter.MaxRoomRows} rooms still have mention traffic");
        text.ShouldNotContain("lane-8");
    }

    [Fact]
    public void Blockers_NameBothEndsAndTheOpenBlockerStatus()
    {
        var text = Format(blockers:
        [
            new Formatter.Blocker("217-next", "216-blocker", PlanningFeatureStatus.InProgress)
        ]);

        text.ShouldContain("217-next waits on 216-blocker [in progress]");
    }

    [Fact]
    public void Blockers_BeyondTheCap_TruncateWithTheHiddenEdgeCount()
    {
        var blockers = new List<Formatter.Blocker>();
        for (var index = 1; index <= 6; index++)
        {
            blockers.Add(new Formatter.Blocker($"f{index}", "gate", PlanningFeatureStatus.Ready));
        }

        var text = Format(blockers: blockers);

        text.ShouldContain($"+{6 - Formatter.MaxBlockerRows} more blocked edges");
        text.ShouldNotContain("f6 waits on");
    }

    [Fact]
    public void EmptyDigest_RendersExactlyTheIdleSentence()
    {
        Format().ShouldBe($"Wave · Overmind\n{Formatter.IdleLine}");
    }

    [Fact]
    public void MissingSection_RendersNoneUnderItsHeader()
    {
        var text = Format(queue: [new Formatter.QueueRow("215-a", FeaturePriority.P2, PlanningFeatureStatus.Draft)]);

        text.ShouldContain("Mentions (24h)\n  none");
        text.ShouldContain("Holders\n  none");
        text.ShouldContain("Blockers\n  none");
        text.ShouldContain("Queue head\n  1. p2 215-a [draft]");
    }

    [Fact]
    public void Digest_CarriesEverySectionHeaderAfterTheProjectName()
    {
        var text = Format(rooms: [new Formatter.RoomTraffic("all", 1, Generated)]);

        text.ShouldStartWith("Wave · Overmind");
        text.ShouldContain("Mentions (24h)");
        text.ShouldContain("Holders");
        text.ShouldContain("Queue head");
        text.ShouldContain("Blockers");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(WaveChatService.MaxHistoryLimit - 1, false)]
    [InlineData(WaveChatService.MaxHistoryLimit, true)]
    [InlineData(WaveChatService.MaxHistoryLimit + 1, true)]
    public void FloorNote_AppearsOnlyWhenTheWindowReadHitsTheCap(int windowRead, bool expected)
    {
        var text = Format(
            rooms: [new Formatter.RoomTraffic("all", 1, Generated)],
            windowRead: windowRead);

        text.Contains(FloorNote).ShouldBe(expected);
    }

    [Fact]
    public void FlooredWindow_NoteSitsUnderTheMentionRowsAndNamesTheCap()
    {
        var text = Format(
            rooms: [new Formatter.RoomTraffic("all", 3, Generated)],
            holders: [new Formatter.Holder("215-a", "tucker", "qwen", AgentSessionLiveness.Alive, null, null)],
            windowRead: WaveChatService.MaxHistoryLimit);

        text.ShouldBe($"""
            Wave · Overmind

            Mentions ({Formatter.MentionWindowLabel})
              all · 3 mentions · last now
            {FloorNote}

            Holders
              215-a · tucker/qwen [alive]

            Queue head
              none

            Blockers
              none
            """);
    }

    [Fact]
    public void FlooredWindow_WithoutMentionRows_KeepsTheNoneRowAndAddsTheNote()
    {
        var text = Format(
            holders: [new Formatter.Holder("215-a", "tucker", null, AgentSessionLiveness.Alive, null, null)],
            windowRead: WaveChatService.MaxHistoryLimit);

        text.ShouldContain($"Mentions ({Formatter.MentionWindowLabel})\n  none\n{FloorNote}\n\nHolders");
    }

    [Fact]
    public void EmptyDigest_FlooredWindow_QualifiesTheIdleLineWithTheFloorNote()
    {
        Format(windowRead: WaveChatService.MaxHistoryLimit)
            .ShouldBe($"Wave · Overmind\n{Formatter.IdleLine}\n{FloorNote}");
    }

    private static string FloorNote =>
        $"  +mention counts cover the first {WaveChatService.MaxHistoryLimit} messages"
        + $" of the {Formatter.MentionWindowLabel} window";

    private static string Format(
        IReadOnlyList<Formatter.RoomTraffic>? rooms = null,
        IReadOnlyList<Formatter.Holder>? holders = null,
        IReadOnlyList<Formatter.QueueRow>? queue = null,
        IReadOnlyList<Formatter.Blocker>? blockers = null,
        int windowRead = 0) =>
        Formatter.Format(new Formatter.Digest(
            "Overmind",
            Generated,
            rooms ?? [],
            holders ?? [],
            queue ?? [],
            blockers ?? [],
            windowRead));
}
