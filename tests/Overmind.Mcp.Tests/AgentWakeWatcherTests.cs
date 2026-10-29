using System.Diagnostics;
using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class AgentWakeWatcherTests : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    private const string OneSignalJson =
        """{"signals":[{"kind":"notification-reply","refId":"14141414-1414-1414-1414-141414141414","roomId":null,"from":"human","createdAt":"2026-10-29T09:00:00Z","excerpt":"ship it"}],"cursor":"2026-10-29T09:00:00Z|14141414-1414-1414-1414-141414141414"}""";

    private const string EmptyJson = """{"signals":[],"cursor":"2026-10-29T09:00:00Z"}""";

    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(WakePeriods.DefaultIdleSeconds);
    private static readonly TimeSpan Gate = TimeSpan.FromSeconds(WakePeriods.DefaultGateSeconds);

    public void Dispose() => AdapterSession.Id = null;

    [Fact]
    public async Task Wait_SignalOnPoll_EscalatesToGate_AndBacksOffWhenQuiet()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new WakePageHandler(OneSignalJson, EmptyJson);
        var slot = new WakeSignalSlot();
        var notifier = new RecordingNotifier();
        var ticker = new ManualTicker();
        var watcher = Start(handler, slot, notifier, ticker);

        await ticker.WaitUntilPeriodCountAsync(1);
        ticker.Periods.ShouldBe([Idle]);

        ticker.Release();
        await notifier.WaitUntilNotifiedAsync(1);
        await ticker.WaitUntilPeriodCountAsync(2);

        ticker.Periods.ShouldBe([Idle, Gate]);
        slot.Consume().ShouldBe(1);

        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(3);

        ticker.Periods.ShouldBe([Idle, Gate, Idle]);

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Wait_WithoutRegisteredSession_NeverCallsTheWakeEndpoint()
    {
        AdapterSession.Id = null;
        var handler = new WakePageHandler(OneSignalJson);
        var ticker = new ManualTicker();
        var watcher = Start(handler, new WakeSignalSlot(), new RecordingNotifier(), ticker);

        await ticker.WaitUntilPeriodCountAsync(1);

        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(2);

        handler.Calls.ShouldBe(0);
        ticker.Periods.ShouldBe([Idle, Idle]);

        await watcher.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public async Task Poll_IdentityRejectedByHost_BackOffQuietlyToIdle(int status)
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new WakeStaticHandler(status);
        var notifier = new RecordingNotifier();
        var ticker = new ManualTicker();
        var watcher = Start(handler, new WakeSignalSlot(), notifier, ticker);

        await ticker.WaitUntilPeriodCountAsync(1);

        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(2);

        handler.Calls.ShouldBe(1);
        ticker.Periods.ShouldBe([Idle, Idle]);
        notifier.Counts.ShouldBeEmpty();

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Poll_EmptySuccessBody_TreatsAsNoSignals()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new WakeStaticHandler(200, "");
        var ticker = new ManualTicker();
        var watcher = Start(handler, new WakeSignalSlot(), new RecordingNotifier(), ticker);

        await ticker.WaitUntilPeriodCountAsync(1);

        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(2);

        ticker.Periods.ShouldBe([Idle, Idle]);

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Poll_UnreachableHost_BackOffToIdle()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new ThrowingWakeHandler();
        var notifier = new RecordingNotifier();
        var ticker = new ManualTicker();
        var watcher = Start(handler, new WakeSignalSlot(), notifier, ticker);

        await ticker.WaitUntilPeriodCountAsync(1);

        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(2);

        ticker.Periods.ShouldBe([Idle, Idle]);
        notifier.Counts.ShouldBeEmpty();

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Poll_SignalsOnConsecutivePasses_AccumulateInTheSlot()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new WakePageHandler(OneSignalJson, OneSignalJson, EmptyJson);
        var slot = new WakeSignalSlot();
        var notifier = new RecordingNotifier();
        var ticker = new ManualTicker();
        var watcher = Start(handler, slot, notifier, ticker);

        await ticker.WaitUntilPeriodCountAsync(1);

        ticker.Release();
        await notifier.WaitUntilNotifiedAsync(1);
        ticker.Release();
        await notifier.WaitUntilNotifiedAsync(2);
        ticker.Release();
        await ticker.WaitUntilPeriodCountAsync(4);

        slot.Consume().ShouldBe(2);
        notifier.Counts.ShouldBe([1, 1]);
        ticker.Periods.ShouldBe([Idle, Gate, Gate, Idle]);

        await watcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_DuringPendingWait_ReturnsBounded()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new WakePageHandler(EmptyJson);
        var ticker = new ManualTicker();
        var watcher = Start(handler, new WakeSignalSlot(), new RecordingNotifier(), ticker);

        var watch = Stopwatch.StartNew();
        await watcher.StopAsync(CancellationToken.None);
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    private static AgentWakeWatcher Start(
        HttpMessageHandler handler,
        WakeSignalSlot slot,
        IWakeNotifier notifier,
        ManualTicker ticker)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:4761") };
        var watcher = new AgentWakeWatcher(new OvermindApiClient(http), slot, notifier, ticker);
        _ = watcher.StartAsync(CancellationToken.None);
        return watcher;
    }

    private sealed class WakePageHandler(params string[] pages) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private int _calls;

        public int Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls;
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            lock (_gate)
            {
                var index = Math.Min(_calls, pages.Length - 1);
                _calls++;
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(pages[index], System.Text.Encoding.UTF8, "application/json")
                };
            }

            return Task.FromResult(response);
        }
    }

    private sealed class WakeStaticHandler(int status = 200, string body = EmptyJson) : HttpMessageHandler
    {
        private readonly object _gate = new();
        private int _calls;

        public int Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls;
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            lock (_gate)
            {
                _calls++;
                response = new HttpResponseMessage((HttpStatusCode)status)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingWakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class RecordingNotifier : IWakeNotifier
    {
        private readonly object _gate = new();
        private readonly List<int> _counts = [];

        public IReadOnlyList<int> Counts
        {
            get
            {
                lock (_gate)
                {
                    return [.. _counts];
                }
            }
        }

        public Task NotifyAsync(int pending, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _counts.Add(pending);
            }

            return Task.CompletedTask;
        }

        public async Task WaitUntilNotifiedAsync(int target)
        {
            var deadline = DateTime.UtcNow.Add(SignalTimeout);
            while (Counts.Count < target)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"notifier recorded {Counts.Count} notifications, expected {target}");
                }

                await Task.Delay(PollInterval);
            }
        }
    }

    private sealed class ManualTicker : IWakeTicker
    {
        private readonly object _gate = new();
        private readonly List<TimeSpan> _periods = [];
        private TaskCompletionSource<bool>? _pending;

        public IReadOnlyList<TimeSpan> Periods
        {
            get
            {
                lock (_gate)
                {
                    return [.. _periods];
                }
            }
        }

        public ValueTask<bool> WaitAsync(TimeSpan period, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> wait;
            lock (_gate)
            {
                _periods.Add(period);
                _pending = wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return new ValueTask<bool>(wait.Task.WaitAsync(cancellationToken));
        }

        public void Release()
        {
            TaskCompletionSource<bool>? wait;
            lock (_gate)
            {
                wait = _pending;
                _pending = null;
            }

            wait?.TrySetResult(true);
        }

        public async Task WaitUntilPeriodCountAsync(int target)
        {
            var deadline = DateTime.UtcNow.Add(SignalTimeout);
            while (Periods.Count < target)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"ticker recorded {Periods.Count} periods, expected {target}");
                }

                await Task.Delay(PollInterval);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
