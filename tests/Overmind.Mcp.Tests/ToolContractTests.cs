using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task ListProjects_ReturnsStubbedProjects()
    {
        var response = await Driver.CallToolAsync("list_projects", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("alpha");
        text.ShouldContain("beta");
    }

    [Fact]
    public async Task ListEntries_AppliesTypeFilterToUpstreamRequest()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate"
        });

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Gate One");

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        var query = ParseQuery(request.Query);
        query["type"].ShouldBe("gate");
    }

    [Fact]
    public async Task ReadEntry_ReturnsStubbedEntry()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Entry One");
        text.ShouldContain("Entry body here.");
    }

    [Fact]
    public async Task WriteEntry_PostsExpectedBodyToApi()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate",
            ["title"] = "New gate",
            ["content"] = "Gate content body.",
            ["tags"] = new JsonArray("review", "ci")
        });

        var request = FindRequest("POST", "/api/entries");
        var body = JsonNode.Parse(request.Body)!;
        body["projectId"]!.GetValue<Guid>().ShouldBe(Canned.ProjectId);
        body["type"]!.GetValue<string>().ShouldBe("gate");
        body["title"]!.GetValue<string>().ShouldBe("New gate");
        body["content"]!.GetValue<string>().ShouldBe("Gate content body.");
        body["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["review", "ci"]);

        var text = Canned.ToolResultText(response);
        text.ShouldContain("New gate");
    }

    [Fact]
    public async Task UpdateEntry_KeepsExistingTitleWhileReplacingContent()
    {
        var response = await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["content"] = "Updated content body."
        });

        var put = FindRequest("PUT", $"/api/entries/{Canned.EntryId}");
        var body = JsonNode.Parse(put.Body)!;
        body["title"]!.GetValue<string>().ShouldBe("Entry One");
        body["content"]!.GetValue<string>().ShouldBe("Updated content body.");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Entry One");
    }

    [Fact]
    public async Task Search_ProxiesFulltextQuery()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello"
        });

        var request = FindRequest("GET", "/api/search");
        var query = ParseQuery(request.Query);
        query["q"].ShouldBe("hello");
        query["mode"].ShouldBe("fulltext");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("hello world snippet");
    }

    [Fact]
    public async Task Search_SemanticUnavailable_MapsToToolError()
    {
        Stub.Route("GET", "/api/search", 503, Canned.SemanticUnavailableJson);

        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
            ["mode"] = "semantic"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("vector search unavailable");
    }

    [Fact]
    public async Task WriteEntry_SkillType_ReturnsToolErrorWithoutPosting()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "skill",
            ["title"] = "A skill",
            ["content"] = "Skill body."
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("Overmind UI");
        text.ShouldContain("note, decision, gate, guard");

        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/entries");
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData("note")]
    [InlineData("decision")]
    [InlineData("gate")]
    [InlineData("guard")]
    public async Task WriteEntry_AllowedTypes_ProxyToApi(string type)
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = type,
            ["title"] = $"{type} entry",
            ["content"] = "body"
        });

        response["result"]!["isError"].ShouldBeNull();
        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/entries");
        JsonNode.Parse(request.Body)!["type"]!.GetValue<string>().ShouldBe(type);
    }

    [Fact]
    public async Task WriteEntry_InvalidType_ReturnsFriendlyToolErrorAndProcessSurvives()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "banana",
            ["title"] = "Nope",
            ["content"] = "body"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("Invalid entry type 'banana'");
        text.ShouldContain("note, decision, gate, guard");

        Driver.HasExited.ShouldBeFalse();
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/entries");
    }

    [Fact]
    public async Task UpdateEntry_SkillEntry_ReturnsToolErrorWithoutPutting()
    {
        var skillEntryId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        Stub.RouteGet($"/api/entries/{skillEntryId}", $$"""
            {"id":"{{skillEntryId}}","projectId":"{{Canned.ProjectId}}","type":"skill","title":"Skill One","content":"x","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
            """);

        var response = await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = skillEntryId.ToString(),
            ["content"] = "changed"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("Overmind UI");

        Stub.Requests.ShouldNotContain(r => r.Method == "PUT" && r.Path == $"/api/entries/{skillEntryId}");
    }

    [Fact]
    public async Task ReadEntry_NotFound_MapsToToolError()
    {
        Stub.Route("GET", $"/api/entries/{Canned.MissingEntryId}", 404, Canned.NotFoundJson);

        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = Canned.MissingEntryId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("was not found");
    }
}
