using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpJournalProfileContractTests : McpSessionTestBase
{
    protected override IReadOnlyDictionary<string, string>? EnvironmentOverrides =>
        new Dictionary<string, string> { [McpProfileResolver.EnvironmentVariable] = "journal" };

    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task Tools_UnderJournalProfile_ListExactlyTheJournalFamily()
    {
        var response = await Driver.ListToolsAsync();

        var listed = response["result"]!["tools"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        listed.ShouldBe(
            ToolDescriptionScanner.ExpectedJournalToolNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            "a journal-profiled adapter advertises exactly the journal family — zero planning tools ride along");
    }

    [Fact]
    public async Task Resources_UnderJournalProfile_ExposeNothing()
    {
        var response = await Driver.ListResourcesAsync();

        AssertListingCarriesNoPlanningSurface(response, "resources", "resources/list");
    }

    [Fact]
    public async Task Prompts_UnderJournalProfile_ExposeNothing()
    {
        var response = await Driver.ListPromptsAsync();

        AssertListingCarriesNoPlanningSurface(response, "prompts", "prompts/list");
    }

    [Fact]
    public void Stderr_NamesTheActiveProfile()
    {
        Driver.Stderr.ShouldContain("[overmind-mcp] MCP profile: journal");
    }

    private static void AssertListingCarriesNoPlanningSurface(JsonNode response, string collection, string method)
    {
        var listed = response["result"]?[$"{collection}"]?.AsArray();
        if (listed is null)
        {
            // Resources and prompts stay unregistered under the journal profile, so the server
            // advertises no capability for those families and refuses the listing outright.
            response["error"]!["code"]!.GetValue<int>().ShouldBe(-32601);
            response["error"]!["message"]!.GetValue<string>().ShouldContain(method);
            return;
        }

        listed.Select(t => t!["name"]!.GetValue<string>())
            .ToList()
            .ShouldBeEmpty($"a journal-profiled adapter exposes zero planning {collection}");
    }
}
