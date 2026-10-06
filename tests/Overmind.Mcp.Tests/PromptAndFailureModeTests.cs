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
    public async Task SessionBootstrap_StatesTheValidationProofFlow()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        var text = PromptText(response);
        text.ShouldContain("overmind_notify(kind=approval, featureSlug)");
        text.ShouldContain("the human's bell reply → planning_validate");
        text.ShouldContain("every edit voids the reply");
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
    public async Task SessionBootstrap_WithSingleProject_CarriesTheWriteCostRoutingLine()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Pick the write by cost: line → planning_append_log; one section / one AC item → planning_patch_artifact; whole body → planning_upsert_artifact; several tasks at once → planning_upsert_tasks.");
    }

    [Fact]
    public async Task SessionBootstrap_WithSingleProject_CarriesTheReadCostRoutingLine()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Pick the read by cost: one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature.");
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

    [Fact]
    public async Task SessionBootstrap_FallbackBranch_CarriesTheWriteCostRoutingLine()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Pick the write by cost: line → planning_append_log; one section / one AC item → planning_patch_artifact; whole body → planning_upsert_artifact; several tasks at once → planning_upsert_tasks.");
    }

    [Fact]
    public async Task SessionBootstrap_FallbackBranch_CarriesTheReadCostRoutingLine()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Pick the read by cost: one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature.");
    }

    [Fact]
    public async Task SessionBootstrap_FallbackBranch_CarriesTheNotifyUrlQuotingLine()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain("quote the url in chat when asking for an approval");
    }

    [Fact]
    public async Task SessionBootstrap_WithSingleProject_CarriesTheNotifyUrlQuotingLine()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain("quote the url in chat when asking for an approval");
    }

    [Fact]
    public async Task SessionBootstrap_FallbackBranch_CarriesTheCeremonyOrderLine()
    {
        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Ceremony order: planning_upsert_artifact(kind=spec) → validate with proof (overmind_notify(kind=approval, featureSlug), wait for the human's reply, planning_validate) → planning_upsert_artifact(kind=plan) → validate with proof again → planning_upsert_tasks execution contracts → work loop (planning_current_task, planning_set_status) → result artifact → the human taps the feature done.");
    }

    [Fact]
    public async Task SessionBootstrap_WithSingleProject_CarriesTheCeremonyOrderLine()
    {
        Stub.RouteGet("/api/projects", $$"""
            [{"id":"{{Canned.ProjectId}}","name":"alpha","diskPath":"E:\\work\\alpha","description":"","createdAt":"2026-09-01T10:00:00Z"}]
            """);

        var response = await Driver.GetPromptAsync("session_bootstrap");

        PromptText(response).ShouldContain(
            "Ceremony order: planning_upsert_artifact(kind=spec) → validate with proof (overmind_notify(kind=approval, featureSlug), wait for the human's reply, planning_validate) → planning_upsert_artifact(kind=plan) → validate with proof again → planning_upsert_tasks execution contracts → work loop (planning_current_task, planning_set_status) → result artifact → the human taps the feature done.");
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
