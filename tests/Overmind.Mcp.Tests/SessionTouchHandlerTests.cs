using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class SessionTouchHandlerTests : IDisposable
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    public void Dispose()
    {
        AdapterSession.Id = null;
    }

    [Fact]
    public async Task Touch_BurstOfCalls_FiresOncePerWindowWithSessionPath()
    {
        var sessionId = Guid.NewGuid();
        AdapterSession.Id = sessionId;
        var handler = new RecordingHandler();
        using var http = Client(handler);

        for (var i = 0; i < 5; i++)
        {
            (await http.GetAsync("/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await handler.WaitUntilTouchCountAsync(1);
        await Task.Delay(200);
        handler.TouchCount.ShouldBe(1);
        var touch = handler.Requests.Single(r => r.Path.EndsWith("/touch", StringComparison.Ordinal));
        touch.Method.ShouldBe(HttpMethod.Post);
        touch.Path.ShouldBe($"/api/agent/sessions/{sessionId}/touch");
    }

    [Fact]
    public async Task Touch_WindowExpires_GateReopensForNextCall()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new RecordingHandler();
        using var http = Client(handler, window: TimeSpan.FromMilliseconds(50));

        await http.GetAsync("/api/health");
        await handler.WaitUntilTouchCountAsync(1);

        await Task.Delay(150);
        await http.GetAsync("/api/health");
        await handler.WaitUntilTouchCountAsync(2);

        handler.TouchCount.ShouldBe(2);
    }

    [Fact]
    public async Task Touch_NoRegisteredSession_NeverFires()
    {
        AdapterSession.Id = null;
        var handler = new RecordingHandler();
        using var http = Client(handler);

        for (var i = 0; i < 3; i++)
        {
            (await http.GetAsync("/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await Task.Delay(200);
        handler.TouchCount.ShouldBe(0);
    }

    [Fact]
    public async Task Touch_EmptySessionId_NeverFires()
    {
        AdapterSession.Id = Guid.Empty;
        var handler = new RecordingHandler();
        using var http = Client(handler);

        await http.GetAsync("/api/health");

        await Task.Delay(200);
        handler.TouchCount.ShouldBe(0);
    }

    [Fact]
    public async Task Touch_FailingTouch_LeavesPipelineResponseUntouched()
    {
        AdapterSession.Id = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/touch", StringComparison.Ordinal)
                ? throw new HttpRequestException("stub touch is down")
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
        using var http = Client(handler);

        using var response = await http.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.TouchCount.ShouldBe(1);
    }

    private static HttpClient Client(RecordingHandler handler, TimeSpan? window = null) =>
        new(window is { } w ? new SessionTouchHandler(handler, w) : new SessionTouchHandler(handler))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
        : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<(HttpMethod Method, string Path)> _requests = [];
        private readonly List<(int Target, TaskCompletionSource Waiter)> _touchWaiters = [];

        public IReadOnlyList<(HttpMethod Method, string Path)> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public int TouchCount
        {
            get
            {
                lock (_gate)
                {
                    return CountTouches();
                }
            }
        }

        public async Task WaitUntilTouchCountAsync(int target)
        {
            Task waiter;
            lock (_gate)
            {
                if (CountTouches() >= target)
                {
                    return;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _touchWaiters.Add((target, completion));
                waiter = completion.Task;
            }

            await waiter.WaitAsync(SignalTimeout);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
                var touches = CountTouches();
                foreach (var (_, waiter) in _touchWaiters.Where(w => w.Target <= touches).ToList())
                {
                    waiter.TrySetResult();
                }

                _touchWaiters.RemoveAll(w => w.Target <= touches);
            }

            if (respond is not null)
            {
                return Task.FromResult(respond(request));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }

        private int CountTouches() =>
            _requests.Count(r => r.Path.EndsWith("/touch", StringComparison.Ordinal));
    }
}
