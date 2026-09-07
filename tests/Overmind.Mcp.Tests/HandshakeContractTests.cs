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
    public async Task StartupToHandshake_CompletesWithinTwoSeconds()
    {
        await Task.CompletedTask;

        StartupToHandshake.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ToolsList_ExposesExactlyTheSixTools()
    {
        var response = await Driver.ListToolsAsync();

        var names = response["result"]!["tools"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        names.ShouldBe(["list_entries", "list_projects", "read_entry", "search", "update_entry", "write_entry"]);
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
