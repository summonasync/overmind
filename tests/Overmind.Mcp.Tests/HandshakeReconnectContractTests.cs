using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class HandshakeReconnectContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.Route("POST", "/api/agent/sessions", _ => new StubOvermindApi.Response(
            200, new JsonObject { ["id"] = Guid.NewGuid().ToString() }.ToJsonString()));
        stub.Route("POST", "/api/agent/sessions/[^/]+/end", 200, "");
    }

    [Fact]
    public async Task ReconnectOnSurvivingProcess_RegistersFreshSession()
    {
        var registrations = () => Stub.Requests.Count(
            r => r.Method == "POST" && r.Path == "/api/agent/sessions");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (registrations() < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        registrations().ShouldBe(1);

        try
        {
            await Driver.CallAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "reconnect-probe", ["version"] = "1.0.0" }
            }, TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
        }

        var reconnectDeadline = DateTime.UtcNow.AddSeconds(5);
        while (registrations() < 2 && DateTime.UtcNow < reconnectDeadline)
        {
            await Task.Delay(50);
        }

        registrations().ShouldBeGreaterThanOrEqualTo(2);

        // The adapter posts /end for the superseded session off the handshake path, so the call lands
        // whenever the retire task gets scheduled — under full-assembly load that is after a bare
        // assert reads the log. Bounded wait, same bar as before.
        var ended = () => Stub.Requests.Count(
            r => r.Method == "POST" && r.Path.EndsWith("/end", StringComparison.Ordinal));

        var endDeadline = DateTime.UtcNow.AddSeconds(5);
        while (ended() < 1 && DateTime.UtcNow < endDeadline)
        {
            await Task.Delay(50);
        }

        ended().ShouldBeGreaterThanOrEqualTo(1);
    }
}
