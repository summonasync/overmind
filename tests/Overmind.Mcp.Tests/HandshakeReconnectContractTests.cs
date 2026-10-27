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
        Stub.Requests.Count(r => r.Method == "POST" && r.Path.EndsWith("/end", StringComparison.Ordinal))
            .ShouldBeGreaterThanOrEqualTo(1);
    }
}
