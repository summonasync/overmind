using System.Net;
using System.Text;
using System.Threading.Channels;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WakeSlotFilterTests
{
    [Theory]
    [InlineData(1, "overmind wake: 1 pending signal — poll overmind_wake_peek for details")]
    [InlineData(3, "overmind wake: 3 pending signals — poll overmind_wake_peek for details")]
    public async Task Pending_SlotCount_IsAppendedToTheResult_AndConsumed(int pending, string expected)
    {
        var slot = new WakeSignalSlot();
        slot.Set(pending);
        var filter = new WakeSlotFilter(slot).Create();

        var result = await InvokeAsync(filter, Result);

        result.Content.Count.ShouldBe(2);
        result.Content[1].ShouldBeOfType<TextContentBlock>().Text.ShouldBe(expected);
        slot.Consume().ShouldBe(0);
    }

    [Fact]
    public async Task Empty_Slot_LeavesTheResultUntouched()
    {
        var filter = new WakeSlotFilter(new WakeSignalSlot()).Create();

        var result = await InvokeAsync(filter, Result);

        result.Content.Single().ShouldBeOfType<TextContentBlock>().Text.ShouldBe("ok");
    }

    [Fact]
    public async Task ReplyConsumed_HostWindowGoesSilent_OnceTheWakePageDrains()
    {
        var previousSession = AdapterSession.Id;
        AdapterSession.Id = Guid.NewGuid();
        var slot = new WakeSignalSlot();
        var filter = new WakeSlotFilter(slot).Create();
        var ticker = new ManualTicker();
        var watcher = new AgentWakeWatcher(
            new OvermindApiClient(
                new HttpClient(new ScriptedWakeHandler(ReplyPageJson, EmptyPageJson))
                {
                    BaseAddress = new Uri("http://127.0.0.1:4761")
                }),
            slot,
            new SilentNotifier(),
            ticker);
        await watcher.StartAsync(CancellationToken.None);

        try
        {
            await ticker.WaitUntilPeriodCountAsync(1);
            ticker.Release();
            await ticker.WaitUntilPeriodCountAsync(2);

            var surfaced = await InvokeAsync(filter, Result);
            surfaced.Content.Count.ShouldBe(2);
            surfaced.Content[1].ShouldBeOfType<TextContentBlock>()
                .Text.ShouldBe("overmind wake: 1 pending signal — poll overmind_wake_peek for details");
            slot.Consume().ShouldBe(0);
            ticker.Periods.ShouldBe([Idle, Gate]);

            ticker.Release();
            await ticker.WaitUntilPeriodCountAsync(3);

            var after = await InvokeAsync(filter, Result);
            after.Content.Single().ShouldBeOfType<TextContentBlock>().Text.ShouldBe("ok");
            ticker.Periods.ShouldBe([Idle, Gate, Idle]);
        }
        finally
        {
            await watcher.StopAsync(CancellationToken.None);
            AdapterSession.Id = previousSession;
        }
    }

    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(WakePeriods.DefaultIdleSeconds);
    private static readonly TimeSpan Gate = TimeSpan.FromSeconds(WakePeriods.DefaultGateSeconds);

    private const string ReplyPageJson =
        """{"signals":[{"kind":"notification-reply","refId":"24442444-2444-2444-2444-244424442444","roomId":null,"from":"human","createdAt":"2026-11-20T09:00:00Z","excerpt":"ship it"}],"cursor":"2026-11-20T09:00:00Z|24442444-2444-2444-2444-244424442444"}""";

    private const string EmptyPageJson = """{"signals":[],"cursor":"2026-11-20T09:00:00Z"}""";

    private static CallToolResult Result() => new()
    {
        Content = [new TextContentBlock { Text = "ok" }]
    };

    private static async Task<CallToolResult> InvokeAsync(
        McpRequestFilter<CallToolRequestParams, CallToolResult> filter,
        Func<CallToolResult> next)
    {
        var context = new RequestContext<CallToolRequestParams>(
            Server(),
            new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call", Params = new System.Text.Json.Nodes.JsonObject() },
            new CallToolRequestParams { Name = "overmind_wake_peek" });

        return await filter((requestContext, _) => new ValueTask<CallToolResult>(next()))(context, CancellationToken.None);
    }

    private static McpServer Server() => McpServer.Create(
        new SilentTransport(),
        new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "wake-filter-test", Version = "0" }
        },
        null,
        null);

    private sealed class SilentTransport : ITransport
    {
        private readonly Channel<JsonRpcMessage> _incoming = Channel.CreateUnbounded<JsonRpcMessage>();

        public string SessionId => "wake-filter-test";

        public ChannelReader<JsonRpcMessage> MessageReader => _incoming.Reader;

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScriptedWakeHandler(params string[] pages) : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var page = pages[Math.Min(_calls, pages.Length - 1)];
            _calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SilentNotifier : IWakeNotifier
    {
        public Task NotifyAsync(int pending, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ManualTicker : IWakeTicker
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

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
