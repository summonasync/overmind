using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpWakeNotifierTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Notify_PendingSignals_WritesLoggingNotificationToTheTransport()
    {
        await using var harness = await SessionHarness.StartAsync();

        await harness.Notifier.NotifyAsync(2, CancellationToken.None);

        var notification = await harness.WaitForNotificationAsync();
        notification.Params!["level"]!.GetValue<string>().ShouldBe("notice");
        notification.Params["logger"]!.GetValue<string>().ShouldBe(McpWakeNotifier.LoggerName);
        notification.Params["data"]!["pending"]!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task Notify_BeforeTheHandshake_SendsNothing()
    {
        await using var harness = await SessionHarness.StartAsync(handshake: false);

        await harness.Notifier.NotifyAsync(1, CancellationToken.None);
        await Task.Delay(100);

        harness.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Notify_ClientFloorAboveNotice_SendsNothing()
    {
        await using var harness = await SessionHarness.StartAsync();
        await harness.CallAsync("logging/setLevel", new JsonObject { ["level"] = "error" });

        await harness.Notifier.NotifyAsync(1, CancellationToken.None);
        await Task.Delay(100);

        harness.Sent.OfType<JsonRpcNotification>().ShouldBeEmpty();
    }

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _run;
        private readonly Task _serverTask;
        private int _calls;

        private SessionHarness(Task serverTask, CancellationTokenSource run)
        {
            _serverTask = serverTask;
            _run = run;
        }

        public RecordingTransport Transport { get; private set; } = default!;

        public McpWakeNotifier Notifier { get; private set; } = default!;

        public IReadOnlyList<JsonRpcMessage> Sent => Transport.Sent;

        public static async Task<SessionHarness> StartAsync(bool handshake = true)
        {
            var transport = new RecordingTransport();
            var run = new CancellationTokenSource();
            var server = McpServer.Create(
                transport,
                new McpServerOptions
                {
                    ServerInfo = new Implementation { Name = "wake-notifier-test", Version = "0" }
                },
                NullLoggerFactory.Instance,
                null);

            var harness = new SessionHarness(server.RunAsync(run.Token), run) { Transport = transport };
            try
            {
                harness.Notifier = new McpWakeNotifier(server);
                if (handshake)
                {
                    await harness.CallAsync("initialize", new JsonObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["capabilities"] = new JsonObject(),
                        ["clientInfo"] = new JsonObject { ["name"] = "wake-notifier-test", ["version"] = "0" }
                    });
                }

                return harness;
            }
            catch
            {
                await harness.DisposeAsync();
                throw;
            }
        }

        public async Task CallAsync(string method, JsonObject parameters)
        {
            _calls++;
            var target = _calls;
            await Transport.FeedAsync(new JsonRpcRequest { Id = new RequestId(target), Method = method, Params = parameters });
            await WaitForResponsesAsync(target);
        }

        public async Task<JsonRpcNotification> WaitForNotificationAsync()
        {
            var deadline = DateTime.UtcNow.Add(SignalTimeout);
            while (true)
            {
                var hit = Sent.OfType<JsonRpcNotification>().FirstOrDefault();
                if (hit is not null)
                {
                    return hit;
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("expected transport message never arrived");
                }

                await Task.Delay(PollInterval);
            }
        }

        private async Task WaitForResponsesAsync(int target)
        {
            var deadline = DateTime.UtcNow.Add(SignalTimeout);
            while (Sent.Count(message => message is JsonRpcResponse) < target)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"expected {target} responses, transport saw fewer");
                }

                await Task.Delay(PollInterval);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _run.Cancel();
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
            }

            _run.Dispose();
        }
    }

    private sealed class RecordingTransport : ITransport
    {
        private readonly Channel<JsonRpcMessage> _incoming = Channel.CreateUnbounded<JsonRpcMessage>();
        private readonly object _gate = new();
        private readonly List<JsonRpcMessage> _sent = [];

        public string SessionId => "wake-notifier-test";

        public ChannelReader<JsonRpcMessage> MessageReader => _incoming.Reader;

        public IReadOnlyList<JsonRpcMessage> Sent
        {
            get
            {
                lock (_gate)
                {
                    return [.. _sent];
                }
            }
        }

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _sent.Add(message);
            }

            return Task.CompletedTask;
        }

        public async Task FeedAsync(JsonRpcRequest request) =>
            await _incoming.Writer.WriteAsync(request);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
