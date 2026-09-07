using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ResourceContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task Projects_Resource_ReturnsProjectListJson()
    {
        var response = await Driver.ReadResourceAsync("ctx://projects");

        var text = Canned.ResourceText(response);
        text.ShouldContain("alpha");
        text.ShouldContain("beta");
    }

    [Fact]
    public async Task Project_Resource_ReturnsProjectMetaJson()
    {
        var response = await Driver.ReadResourceAsync($"ctx://projects/{Canned.ProjectId}");

        var text = Canned.ResourceText(response);
        text.ShouldContain("\"alpha\"");
        text.ShouldContain("""E:\\work\\alpha""");
    }

    [Theory]
    [InlineData("gates", "gate", "Gate One")]
    [InlineData("guards", "guard", "Guard One")]
    [InlineData("notes", "note", "Note One")]
    [InlineData("decisions", "decision", "Decision One")]
    [InlineData("skills", "skill", "Skill One")]
    public async Task ProjectTypedResources_ReturnConcatenatedMarkdownWithTitleHeaders(
        string segment, string type, string title)
    {
        var response = await Driver.ReadResourceAsync($"ctx://projects/{Canned.ProjectId}/{segment}");

        var text = Canned.ResourceText(response);
        text.ShouldContain($"# {title}");

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        var query = ParseQuery(request.Query);
        query["type"].ShouldBe(type);
    }

    [Fact]
    public async Task Gates_Resource_ConcatenatesAllStubbedEntries()
    {
        var response = await Driver.ReadResourceAsync($"ctx://projects/{Canned.ProjectId}/gates");

        var text = Canned.ResourceText(response);
        text.ShouldContain("# Gate One");
        text.ShouldContain("Always run the full test suite.");
        text.ShouldContain("# Gate Two");
        text.ShouldContain("Never skip review.");
    }

    [Fact]
    public async Task Entry_Resource_ReturnsMarkdownWithTitleAndContent()
    {
        var response = await Driver.ReadResourceAsync($"ctx://entries/{Canned.EntryId}");

        var text = Canned.ResourceText(response);
        text.ShouldContain("# Entry One");
        text.ShouldContain("Entry body here.");
    }

    [Fact]
    public async Task Entry_NotFound_MapsToResourceNotFoundError()
    {
        Stub.Route("GET", $"/api/entries/{Canned.MissingEntryId}", 404, Canned.NotFoundJson);

        var response = await Driver.ReadResourceAsync($"ctx://entries/{Canned.MissingEntryId}");

        var error = response["error"];
        error.ShouldNotBeNull("expected a JSON-RPC error, got: " + response.ToJsonString());
        error!["code"]!.GetValue<long>().ShouldBe(-32002);
        error["message"]!.GetValue<string>().ShouldContain("was not found");
    }
}
