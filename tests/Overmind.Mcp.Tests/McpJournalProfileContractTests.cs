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
    public async Task Tools_UnderJournalProfile_ExposeZeroPlanningTools()
    {
        var response = await Driver.ListToolsAsync();

        AssertListingCarriesNoPlanningSurface(response, "tools", "tools/list");
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
            // Until T1/T2 land, the journal surface registers nothing, so the server advertises no
            // capability for the family and refuses the listing outright; the journal registrations
            // then move this assertion onto the normal listing carrying exactly the journal family.
            response["error"]!["code"]!.GetValue<int>().ShouldBe(-32601);
            response["error"]!["message"]!.GetValue<string>().ShouldContain(method);
            return;
        }

        listed.Select(t => t!["name"]!.GetValue<string>())
            .ToList()
            .ShouldBeEmpty($"a journal-profiled adapter exposes zero planning {collection}");
    }
}
