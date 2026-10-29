using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class AgentWakeWatcherContractTests : McpSessionTestBase
{
    private static readonly Guid SessionId = Guid.Parse("19191919-1919-1919-1919-191919191919");
    private static readonly Guid SignalRefId = Guid.Parse("17171717-1717-1717-1717-171717171717");

    private static readonly string WakeJson =
        $$"""{"signals":[{"kind":"notification-reply","refId":"{{SignalRefId}}","roomId":null,"from":"human","createdAt":"2026-10-29T09:00:00Z","excerpt":"ship it"}],"cursor":"2026-10-29T09:00:00Z|{{SignalRefId}}"}""";

    protected override IReadOnlyDictionary<string, string> EnvironmentOverrides => new Dictionary<string, string>
    {
        [WakePeriods.IdleEnvironmentVariable] = "5",
        [WakePeriods.GateEnvironmentVariable] = "1"
    };

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.Route("POST", "/api/agent/sessions", _ => new StubOvermindApi.Response(
            200, new JsonObject { ["id"] = SessionId.ToString() }.ToJsonString()));
        stub.Route("POST", "/api/agent/sessions/[^/]+/end", 200, "");
        stub.RouteGet("/api/agent/wake", WakeJson);
    }

    [Fact]
    public async Task Adapter_EscalatesOnSignal_NotifiesOverStdio_AndSurfacesThePendingCount()
    {
        await WaitUntilWakeCallsAsync(1);
        var firstPoll = DateTime.UtcNow;

        await WaitUntilWakeCallsAsync(2);
        (DateTime.UtcNow - firstPoll).ShouldBeLessThan(TimeSpan.FromSeconds(4));

        var wakeRequest = Stub.Requests.First(r => r.Method == "GET" && r.Path == "/api/agent/wake");
        wakeRequest.Headers[AdapterSession.HeaderName].ShouldBe(SessionId.ToString());

        var notification = await WaitUntilWakeNotificationAsync();
        notification["level"]!.GetValue<string>().ShouldBe("notice");
        notification["logger"]!.GetValue<string>().ShouldBe(McpWakeNotifier.LoggerName);
        notification["data"]!["pending"]!.GetValue<int>().ShouldBeGreaterThanOrEqualTo(1);

        var call = await Driver.CallToolAsync("overmind_wake_peek", new JsonObject());
        var text = string.Concat(
            call["result"]!["content"]!.AsArray().Select(block => block!["text"]?.GetValue<string>() ?? string.Empty));

        text.ShouldContain("overmind wake:");
        text.ShouldContain("pending signal");
    }

    private async Task WaitUntilWakeCallsAsync(int target)
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (Stub.Requests.Count(r => r.Method == "GET" && r.Path == "/api/agent/wake") < target)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"wake endpoint called {Stub.Requests.Count(r => r.Method == "GET" && r.Path == "/api/agent/wake")} times, expected {target}");
            }

            await Task.Delay(50);
        }
    }

    private async Task<JsonObject> WaitUntilWakeNotificationAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (true)
        {
            var line = Driver.StdoutLines.FirstOrDefault(line =>
            {
                try
                {
                    return JsonNode.Parse(line)?["method"]?.GetValue<string>() == "notifications/message";
                }
                catch (System.Text.Json.JsonException)
                {
                    return false;
                }
            });

            if (line is not null)
            {
                return JsonNode.Parse(line)!["params"]!.AsObject();
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("no notifications/message line appeared on stdout");
            }

            await Task.Delay(50);
        }
    }
}
