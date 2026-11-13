using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class EntryFilterToolContractTests : McpSessionTestBase
{
    private static readonly string EntriesPath = $"/api/projects/{Canned.ProjectId}/entries";

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);

        stub.Route("GET", EntriesPath, req =>
        {
            var until = ParseQuery(req.Query).GetValueOrDefault("until");
            return until == "not-a-date"
                ? new StubOvermindApi.Response(400, InvalidDateProblem("until", until))
                : new StubOvermindApi.Response(200, "[]");
        });

        stub.Route("GET", "/api/search", req =>
        {
            var since = ParseQuery(req.Query).GetValueOrDefault("since");
            return since == "not-a-date"
                ? new StubOvermindApi.Response(400, InvalidDateProblem("since", since))
                : new StubOvermindApi.Response(200, Canned.SearchJson);
        });
    }

    [Fact]
    public async Task ListEntries_DateAndAuthorFilters_RideTheUpstreamQueryVerbatim()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["since"] = "2026-11-01",
            ["until"] = "2026-11-12T09:30:00Z",
            ["author"] = "billy-jr"
        });

        var query = ParseQuery(FindRequest("GET", EntriesPath).Query);
        query["since"].ShouldBe("2026-11-01");
        query["until"].ShouldBe("2026-11-12T09:30:00Z");
        query["author"].ShouldBe("billy-jr");
    }

    [Fact]
    public async Task ListEntries_WithoutFilters_LeavesDateAndAuthorOffTheQuery()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "note"
        });

        var query = ParseQuery(FindRequest("GET", EntriesPath).Query);
        query.ShouldNotContainKey("since");
        query.ShouldNotContainKey("until");
        query.ShouldNotContainKey("author");
    }

    [Fact]
    public async Task ListEntries_InvalidUntil_SurfacesUpstreamValidationProblem()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["until"] = "not-a-date"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("overmind API returned 400 (GET " + EntriesPath);
        text.ShouldContain("until: ");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Search_DateAndAuthorFilters_RideTheUpstreamQueryVerbatim()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
            ["since"] = "2026-11-01",
            ["until"] = "2026-11-12",
            ["author"] = "Dan"
        });

        response["result"]!["isError"].ShouldBeNull();

        var query = ParseQuery(FindRequest("GET", "/api/search").Query);
        query["since"].ShouldBe("2026-11-01");
        query["until"].ShouldBe("2026-11-12");
        query["author"].ShouldBe("Dan");
    }

    [Fact]
    public async Task Search_WithoutFilters_LeavesDateAndAuthorOffTheQuery()
    {
        await Driver.CallToolAsync("search", new JsonObject { ["query"] = "hello" });

        var query = ParseQuery(FindRequest("GET", "/api/search").Query);
        query.ShouldNotContainKey("since");
        query.ShouldNotContainKey("until");
        query.ShouldNotContainKey("author");
    }

    [Fact]
    public async Task Search_InvalidSince_SurfacesUpstreamValidationProblem()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
            ["since"] = "not-a-date"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("overmind API returned 400 (GET /api/search");
        text.ShouldContain("since: ");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task ToolsList_FiltersPinSchemaAndWriteEntryCarriesNoMirrorClaim()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var listDescription = tools["list_entries"]!["description"]!.GetValue<string>();
        listDescription.ShouldContain("since/until (createdAt bounds) and author");

        var listProperties = tools["list_entries"]!["inputSchema"]!["properties"]!.AsObject();
        listProperties.ShouldContainKey("since");
        listProperties.ShouldContainKey("until");
        listProperties.ShouldContainKey("author");

        var searchProperties = tools["search"]!["inputSchema"]!["properties"]!.AsObject();
        searchProperties.ShouldContainKey("since");
        searchProperties.ShouldContainKey("until");
        searchProperties.ShouldContainKey("author");

        var writeDescription = tools["write_entry"]!["description"]!.GetValue<string>();
        writeDescription.ShouldNotContain("mirror");

        var writeProperties = tools["write_entry"]!["inputSchema"]!["properties"]!.AsObject();
        writeProperties.ShouldNotContainKey("mirror");
    }

    private static string InvalidDateProblem(string field, string value) =>
        $$$"""
        {"title":"One or more validation errors occurred.","status":400,"errors":{"{{{field}}}":["The supplied {{{field}}} value '{{{value}}}' is not a valid date or timestamp."]}}
        """;

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
