using System.Diagnostics;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class StartupReadinessGateContractTests : McpSessionTestBase
{
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(60);

    private readonly TaskCompletionSource _hostReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override bool AutoHandshake => false;

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.Route("GET", "/api/health", _ => _hostReady.Task.IsCompleted
            ? new StubOvermindApi.Response(200, Canned.HealthJson)
            : new StubOvermindApi.Response(503, """{"title":"Starting"}"""));
        stub.Route("POST", "/api/agent/sessions", _ => new StubOvermindApi.Response(
            200, new JsonObject { ["id"] = Guid.NewGuid().ToString() }.ToJsonString()));
        stub.Route("POST", "/api/agent/sessions/[^/]+/end", 200, "");
    }

    [Fact]
    public async Task Initialize_DuringHostStartup_WaitsAndAnswersAfterReady()
    {
        var initialize = Driver.InitializeAsync();

        await WaitForHealthProbeAsync();
        var probesBeforeReady = HealthProbeCount();
        probesBeforeReady.ShouldBeGreaterThanOrEqualTo(1);

        _hostReady.TrySetResult();

        var result = await initialize.WaitAsync(ProbeDeadline);

        result["result"]!["serverInfo"]!["name"]!.GetValue<string>().ShouldBe("overmind");
        Driver.HasExited.ShouldBeFalse();
        HealthProbeCount().ShouldBeGreaterThan(probesBeforeReady);
        Driver.AssertStdoutPurity();
    }

    private async Task WaitForHealthProbeAsync()
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < ProbeDeadline)
        {
            if (HealthProbeCount() > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("adapter never probed the stub health endpoint");
    }

    private int HealthProbeCount() =>
        Stub.Requests.Count(r => r.Method == "GET" && r.Path == "/api/health");
}
