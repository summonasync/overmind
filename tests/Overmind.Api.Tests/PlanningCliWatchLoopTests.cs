using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCliWatchLoopTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly string StartIso = "2026-11-12T09:00:00.0000000+00:00";

    [Fact]
    public async Task RunAsync_FirstPollCarriesBaselineSinceAndSkipsOlderSignals()
    {
        var old = Signal(PlanningCliWatchOptions.ReplyKind, "human", Start.AddSeconds(-30), null);
        var fresh = Signal(
            PlanningCliWatchOptions.MentionKind, "van", Start.AddSeconds(5), "lane-a");
        var harness = Harness(
            new PlanningCliWatchPage([old, fresh], "page-1"),
            options: Both(TimeSpan.FromMinutes(30)));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Requested.ShouldBe([StartIso]);
        harness.Output.ShouldContain("[wave-mention] from van in lane-a");
        harness.Output.ShouldNotContain("notification-reply");
        harness.Output.ShouldNotContain(Start.AddSeconds(-30).ToString("O"));
    }

    [Fact]
    public async Task RunAsync_WithMentionsFilter_RendersOnlyMentionKind()
    {
        var at = Start.AddSeconds(2);
        var harness = Harness(
            new PlanningCliWatchPage(
                [
                    Signal(PlanningCliWatchOptions.ReplyKind, "human", Start.AddSeconds(1), null),
                    Signal(PlanningCliWatchOptions.MentionKind, "kerrigan", at, "all"),
                ],
                "cursor-a"),
            options: new PlanningCliWatchOptions(
                null, PlanningCliWatchOptions.MentionKind, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10)));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Output.ShouldBe($"[wave-mention] from kerrigan in all at {at:O}: wake up\n");
    }

    [Fact]
    public async Task RunAsync_WithRepliesFilter_RendersOnlyReplyKind()
    {
        var at = Start.AddSeconds(2);
        var harness = Harness(
            new PlanningCliWatchPage(
                [
                    Signal(PlanningCliWatchOptions.MentionKind, "kerrigan", Start.AddSeconds(1), "lane-b"),
                    Signal(PlanningCliWatchOptions.ReplyKind, "human", at, null),
                ],
                "cursor-a"),
            options: new PlanningCliWatchOptions(
                null, PlanningCliWatchOptions.ReplyKind, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10)));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Output.ShouldBe($"[notification-reply] from human in - at {at:O}: wake up\n");
    }

    [Fact]
    public async Task RunAsync_PagesOnCursorUntilMatchingSignalArrives()
    {
        var cursor = "2026-11-12T09:00:05.0000000+00:00|0e1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d";
        var harness = Harness(
            new PlanningCliWatchPage(
                [Signal(PlanningCliWatchOptions.ReplyKind, "human", Start.AddSeconds(3), null)],
                cursor),
            new PlanningCliWatchPage(
                [Signal(PlanningCliWatchOptions.MentionKind, "billy", Start.AddSeconds(8), "lane-b")],
                "cursor-final"),
            options: new PlanningCliWatchOptions(
                null, PlanningCliWatchOptions.MentionKind, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10)));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Requested.ShouldBe([StartIso, cursor]);
        harness.Output.ShouldContain("[wave-mention] from billy in lane-b");
    }

    [Fact]
    public async Task RunAsync_EmptyPagesUntilDeadline_PrintsNoSignalsAndExitsZero()
    {
        var harness = Harness(new PlanningCliWatchPage([], "still-empty"), options: new(
            null, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Requested.Count.ShouldBe(2);
        harness.Delays.ShouldBe([TimeSpan.FromMinutes(10)]);
        harness.Output.ShouldBe("no signals within 10m\n");
    }

    [Fact]
    public async Task RunAsync_ZeroTimeout_TimesOutAfterFirstPollWithoutSleeping()
    {
        var harness = Harness(new PlanningCliWatchPage([], "cursor"), options: Both(TimeSpan.Zero));

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(0);
        harness.Requested.Count.ShouldBe(1);
        harness.Delays.ShouldBeEmpty();
        harness.Output.ShouldBe("no signals within 0s\n");
    }

    [Fact]
    public async Task RunAsync_CancelledWhileWaiting_Exits130WithoutOutput()
    {
        var harness = Harness(new PlanningCliWatchPage([], "cursor"), options: Both(TimeSpan.FromMinutes(30)));
        harness.OnDelay = (_, _) => throw new OperationCanceledException();

        var code = await harness.Loop.RunAsync(harness.Options, CancellationToken.None);

        code.ShouldBe(PlanningCliWatchLoop.InterruptedExitCode);
        harness.Output.ShouldBeEmpty();
    }

    private static WatchHarness Harness(
        PlanningCliWatchPage page,
        PlanningCliWatchOptions options)
    {
        var pages = new Queue<PlanningCliWatchPage>([page, page, page, page, page]);
        return Harness(pages, options);
    }

    private static WatchHarness Harness(
        PlanningCliWatchPage first,
        PlanningCliWatchPage second,
        PlanningCliWatchOptions options)
    {
        var pages = new Queue<PlanningCliWatchPage>([first, second, second, second]);
        return Harness(pages, options);
    }

    private static WatchHarness Harness(Queue<PlanningCliWatchPage> pages, PlanningCliWatchOptions options)
    {
        var harness = new WatchHarness(options);
        harness.Loop = new PlanningCliWatchLoop(
            (since, _) =>
            {
                harness.Requested.Add(since!);
                return Task.FromResult(pages.Count > 1 ? pages.Dequeue() : pages.Peek());
            },
            (interval, _) =>
            {
                harness.Delays.Add(interval);
                harness.OnDelay?.Invoke(interval, CancellationToken.None);
                harness.Now += interval;
                return Task.CompletedTask;
            },
            () => harness.Now,
            harness.Writer);
        return harness;
    }

    private static PlanningCliWatchOptions Both(TimeSpan timeout) =>
        new(null, null, timeout, TimeSpan.FromSeconds(10));

    private static PlanningCliWatchSignal Signal(
        string kind, string from, DateTimeOffset createdAt, string? room) =>
        new(kind, Guid.NewGuid(), room, from, createdAt, "wake up");

    private sealed class WatchHarness(PlanningCliWatchOptions options)
    {
        public PlanningCliWatchLoop Loop { get; set; } = null!;
        public List<string> Requested { get; } = [];
        public List<TimeSpan> Delays { get; } = [];
        public DateTimeOffset Now { get; set; } = Start;
        public Action<TimeSpan, CancellationToken>? OnDelay { get; set; }
        public StringWriter Writer { get; } = new();
        public PlanningCliWatchOptions Options { get; } = options;

        public string Output => Writer.ToString();
    }
}
