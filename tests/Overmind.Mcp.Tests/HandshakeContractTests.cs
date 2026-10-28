using System.Diagnostics;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class HandshakeContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task Initialize_ReportsServerInfo_AndSupportsToolsResourcesPrompts()
    {
        var result = await Driver.InitializeAsync();

        var serverInfo = result["result"]!["serverInfo"]!;
        serverInfo["name"]!.GetValue<string>().ShouldBe("overmind");

        var expectedVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                typeof(Overmind.Mcp.ApiBaseUrl).Assembly)!
            .InformationalVersion;
        serverInfo["version"]!.GetValue<string>().ShouldBe(expectedVersion);

        var capabilities = result["result"]!["capabilities"]!;
        capabilities["tools"].ShouldNotBeNull();
        capabilities["resources"].ShouldNotBeNull();
        capabilities["prompts"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Initialize_Instructions_TeachBriefingFirst_NeverTheDeadResourcesUri()
    {
        var result = await Driver.InitializeAsync();

        var instructions = result["result"]!["instructions"]!.GetValue<string>();
        instructions.ShouldNotContain("overmind://resources");
        instructions.ShouldContain("overmind_onboard");
        instructions.ShouldContain("Claim it first with planning_claim_feature");
        instructions.ShouldContain("onboarding quickstart explains the workflow");
        instructions.ShouldContain("overmind_briefing returns the whole guardrail picture in one call");
        instructions.IndexOf("overmind_briefing", StringComparison.Ordinal)
            .ShouldBeLessThan(instructions.IndexOf("list_entries", StringComparison.Ordinal));
        instructions.ShouldContain("list_entries");
        instructions.ShouldContain("quote the url in chat when asking for an approval");
    }

    [Fact]
    public async Task StartupToHandshake_CompletesWithinTwoSeconds()
    {
        await Task.CompletedTask;

        StartupToHandshake.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void StartupCalls_CarryAdapterVersionHeader_OnEveryHostRequest()
    {
        Stub.Requests.ShouldNotBeEmpty();
        foreach (var request in Stub.Requests)
        {
            request.Headers.TryGetValue(AdapterVersion.HeaderName, out var value)
                .ShouldBeTrue($"{request.Method} {request.Path} is missing the adapter version header");
            value.ShouldBe(AdapterVersion.Current);
        }
    }

    [Fact]
    public async Task ToolsList_ExposesExactlyTheSixtyTools()
    {
        var response = await Driver.ListToolsAsync();

        var names = response["result"]!["tools"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        names.ShouldBe([
            "ack_entry",
            "delete_entry",
            "issues_append",
            "issues_create", "issues_get", "issues_list", "issues_promote", "issues_set_status",
            "list_entries", "list_projects", "overmind_briefing", "overmind_notify", "overmind_onboard",
            "overmind_replies", "overmind_wave_snapshot",
            "planning_ack_directive", "planning_add_feature_dependency", "planning_append_directive",
            "planning_append_log",
            "planning_claim_feature",
            "planning_create_feature", "planning_current_task",
            "planning_delete_artifact", "planning_edit_log", "planning_export_backlog", "planning_feature_code", "planning_import_feature",
            "planning_list_artifacts", "planning_list_feature_dependencies", "planning_list_features", "planning_list_lanes",
            "planning_materialize", "planning_materialize_status", "planning_move_feature",
            "planning_patch_artifact",
            "planning_pin_feature_code",
            "planning_read_artifact", "planning_read_artifact_diff", "planning_read_feature",
            "planning_remove_feature_dependency", "planning_remove_lane",
            "planning_set_feature_priority", "planning_set_feature_status", "planning_set_lanes", "planning_set_status",
            "planning_task_queue", "planning_unclaim_feature", "planning_update_feature",
            "planning_upsert_artifact",
            "planning_upsert_tasks", "planning_validate",
            "read_entry", "read_project_manifest", "search", "session_identity_set", "update_entry",
            "wave_chat_peek", "wave_chat_post", "wave_chat_unread", "wave_role_set", "write_entry"
        ]);
    }

    [Fact]
    public async Task ResourcesList_ExposesAllCtxUris()
    {
        var staticResponse = await Driver.ListResourcesAsync();
        var staticUris = staticResponse["result"]!["resources"]!.AsArray()
            .Select(r => r!["uri"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        staticUris.ShouldContain("overmind://projects");

        var templatesResponse = await Driver.CallAsync("resources/templates/list", new JsonObject());
        var templateUris = templatesResponse["result"]!["resourceTemplates"]!.AsArray()
            .Select(t => t!["uriTemplate"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        templateUris.ShouldContain("overmind://projects/{id}");
        templateUris.ShouldContain("overmind://projects/{id}/gates");
        templateUris.ShouldContain("overmind://projects/{id}/guards");
        templateUris.ShouldContain("overmind://projects/{id}/notes");
        templateUris.ShouldContain("overmind://projects/{id}/decisions");
        templateUris.ShouldContain("overmind://projects/{id}/skills");
        templateUris.ShouldContain("overmind://projects/{id}/planning/features");
        templateUris.ShouldContain("overmind://projects/{id}/planning/features/{slug}");
        templateUris.ShouldContain("overmind://projects/{id}/planning/features/{slug}/current");
        templateUris.ShouldContain("overmind://entries/{id}");
    }

    [Fact]
    public async Task PromptsList_ExposesSessionBootstrap()
    {
        var response = await Driver.ListPromptsAsync();

        var names = response["result"]!["prompts"]!.AsArray()
            .Select(p => p!["name"]!.GetValue<string>())
            .ToArray();

        names.ShouldBe(["session_bootstrap"]);
    }
}
