using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class SessionHeartbeatTests : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);

    public void Dispose() => AdapterSession.Id = null;

    [Fact]
    public async Task Ticks_EveryPeriod_PostTouchForRegisteredSession()
    {
        var sessionId = Guid.NewGuid();
        AdapterSession.Id = sessionId;
        var handler = new RecordingHandler();
        using var http = Client(handler);
        var heartbeat = Start(http);

        await handler.WaitUntilTouchCountAsync(2);
        heartbeat.Dispose();

        var touches = handler.Touches;
        touches.Count.ShouldBeGreaterThanOrEqualTo(2);
        touches.ShouldAllBe(t => t.Method == HttpMethod.Post);
        touches.ShouldAllBe(t => t.Path == $"/api/agent/sessions/{sessionId}/touch");
    }

    [Fact]
    public async Task Ticks_NoRegisteredSession_NeverFires()
    {
        AdapterSession.Id = null;
        var handler = new RecordingHandler();
        using var http = Client(handler);
        var heartbeat = Start(http);

        await Task.Delay(QuietPeriod);
        heartbeat.Dispose();

        handler.Touches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_StopsTicks_CountStaysFlat()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new RecordingHandler();
        using var http = Client(handler);
        var heartbeat = Start(http);

        await handler.WaitUntilTouchCountAsync(1);
        heartbeat.Dispose();
        var afterStop = handler.Touches.Count;

        await Task.Delay(QuietPeriod);

        handler.Touches.Count.ShouldBe(afterStop);
    }

    [Fact]
    public async Task Dispose_DuringSlowTouch_BoundedWait()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new SlowTouchHandler();
        using var http = Client(handler);
        var heartbeat = Start(http);

        await handler.WaitUntilTouchStartedAsync();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        heartbeat.Dispose();
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(SessionTouchHandler.TouchTimeout + TimeSpan.FromSeconds(2));
    }

    private static SessionHeartbeat Start(HttpClient http)
    {
        var heartbeat = new SessionHeartbeat(http, Tick);
        heartbeat.Start();
        return heartbeat;
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<(HttpMethod Method, string Path)> _touches = [];
        private readonly List<(int Target, TaskCompletionSource Waiter)> _waiters = [];

        public IReadOnlyList<(HttpMethod Method, string Path)> Touches
        {
            get
            {
                lock (_gate)
                {
                    return [.. _touches];
                }
            }
        }

        public async Task WaitUntilTouchCountAsync(int target)
        {
            Task waiter;
            lock (_gate)
            {
                if (_touches.Count >= target)
                {
                    return;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((target, completion));
                waiter = completion.Task;
            }

            await waiter.WaitAsync(SignalTimeout);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _touches.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
                foreach (var (_, waiter) in _waiters.Where(w => w.Target <= _touches.Count).ToList())
                {
                    waiter.TrySetResult();
                }

                _waiters.RemoveAll(w => w.Target <= _touches.Count);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class SlowTouchHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitUntilTouchStartedAsync()
        {
            await Started.Task.WaitAsync(SignalTimeout);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(30_000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
