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
    public async Task Initialize_Instructions_PointAtListEntries_NeverAtTheDeadResourcesUri()
    {
        var result = await Driver.InitializeAsync();

        var instructions = result["result"]!["instructions"]!.GetValue<string>();
        instructions.ShouldNotContain("overmind://resources");
        instructions.ShouldContain("list_entries");
        instructions.ShouldContain("overmind_onboard");
    }

    [Fact]
    public async Task StartupToHandshake_CompletesWithinTwoSeconds()
    {
        await Task.CompletedTask;

        StartupToHandshake.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ToolsList_ExposesExactlyTheThirtyFourTools()
    {
        var response = await Driver.ListToolsAsync();

        var names = response["result"]!["tools"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        names.ShouldBe([
            "delete_entry", "list_entries", "list_projects", "overmind_notify", "overmind_onboard",
            "overmind_replies", "planning_add_feature_dependency",
            "planning_append_log",
            "planning_create_feature", "planning_current_task", "planning_import_feature",
            "planning_list_artifacts", "planning_list_feature_dependencies", "planning_list_features",
            "planning_materialize", "planning_materialize_status", "planning_move_feature",
            "planning_patch_artifact",
            "planning_read_artifact", "planning_read_feature",
            "planning_remove_feature_dependency",
            "planning_set_current",
            "planning_set_feature_priority", "planning_set_feature_status", "planning_set_status",
            "planning_task_queue", "planning_update_feature", "planning_upsert_artifact",
            "planning_upsert_tasks", "planning_validate",
            "read_entry", "search", "update_entry", "write_entry"
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
