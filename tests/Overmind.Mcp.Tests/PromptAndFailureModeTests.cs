using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class PromptContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        stub.RouteGet("/api/health", Canned.HealthJson);
        stub.RouteGet("/api/projects", Canned.ProjectsJson);
    }

    [Fact]
    public async Task SessionBootstrap_WithTwoProjects_ReturnsListingInstructions()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        var text = PromptText(response);
        text.ShouldContain("read overmind://projects to list available projects");
        text.ShouldContain("OVERMIND_PROJECT_ID");
    }

    [Fact]
    public async Task SessionBootstrap_WithSingleProject_PointsAtItsGatesAndGuards()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        var text = PromptText(response);
        text.ShouldContain($"read overmind://projects/{Canned.ProjectId}/gates and /guards and follow them");
    }

    [Fact]
    public async Task SessionBootstrap_WithSingleProject_TeachesThePlanningLoop()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        var text = PromptText(response);
        text.ShouldContain($"overmind://projects/{Canned.ProjectId}/planning/features");
        text.ShouldContain("planning/features/<slug>/current");
        text.ShouldContain("STOP");
        text.ShouldContain("planning_set_status");
        text.ShouldContain("planning_append_log");
    }

    [Fact]
    public async Task SessionBootstrap_FallbackBranch_TeachesThePlanningLoop()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        var text = PromptText(response);
        text.ShouldContain("overmind://projects/{id}/planning/features");
        text.ShouldContain("planning/features/<slug>/current");
        text.ShouldContain("STOP");
        text.ShouldContain("planning_set_status");
        text.ShouldContain("planning_append_log");
    }

    private static string PromptText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        var message = result!["messages"]!.AsArray().First()!;
        return message["content"]!["text"]!.GetValue<string>();
    }
}

public class FailureModeTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task ApiUnreachableAtStartup_ExitsWithCodeTwoAndStderrMessage()
    {
        using var driver = McpProcessDriver.Start($"http://127.0.0.1:{DeadPort()}");

        var exitCode = await driver.WaitForExitAsync(TimeSpan.FromSeconds(30));

        exitCode.ShouldBe(2);
        driver.Stderr.ShouldContain("unreachable");
        driver.StdoutLines.ShouldBeEmpty();
        driver.AssertStdoutPurity();
    }

    [Fact]
    public async Task ApiGoingDownMidSession_ToolErrorsWithoutCrashingProcess()
    {
        var before = await Driver.CallToolAsync("list_projects", new JsonObject());
        before["error"].ShouldBeNull();

        Stub.Stop();

        var response = await Driver.CallToolAsync("list_projects", new JsonObject());
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("unreachable");

        Driver.HasExited.ShouldBeFalse();

        var second = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });
        second["result"]!["isError"]!.GetValue<bool>().ShouldBeTrue();
        Driver.HasExited.ShouldBeFalse();
    }

    private static int DeadPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
