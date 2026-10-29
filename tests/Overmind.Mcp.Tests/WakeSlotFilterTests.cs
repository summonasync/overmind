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
        slot.Add(pending);
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
}
