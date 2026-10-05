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
    public async Task ListEntries_TombstoneType_PassesThroughAndProjectsLikeAnyEntry()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "tombstone"
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query)["type"].ShouldBe("tombstone");

        var entry = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray().Single()!.AsObject();
        entry["type"]!.GetValue<string>().ShouldBe("tombstone");
        entry["title"]!.GetValue<string>().ShouldBe("Decision One (deleted)");
        entry["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["buried"]);
    }

    [Fact]
    public async Task ListEntries_Default_ProjectsMetadataAndDropsBodies()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate"
        });

        var text = Canned.ToolResultText(response);
        var entries = JsonNode.Parse(text)!.AsArray();
        entries.Count.ShouldBe(2);

        var first = entries[0]!.AsObject();
        first["id"]!.GetValue<string>().ShouldBe("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        first["type"]!.GetValue<string>().ShouldBe("gate");
        first["title"]!.GetValue<string>().ShouldBe("Gate One");
        first["tags"]!.AsArray().Count.ShouldBe(0);
        first["createdAt"]!.GetValue<string>().ShouldBe("2026-09-01T10:00:00Z");
        first["updatedAt"]!.GetValue<string>().ShouldBe("2026-09-01T10:00:00Z");
        first["contentLength"]!.GetValue<int>().ShouldBe("Always run the full test suite.".Length);

        var second = entries[1]!.AsObject();
        second["title"]!.GetValue<string>().ShouldBe("Gate Two");
        second["contentLength"]!.GetValue<int>().ShouldBe("Never skip review.".Length);

        entries.ShouldAllBe(e => !e!.AsObject().ContainsKey("content"));
        text.ShouldNotContain("Always run the full test suite.");
        text.ShouldNotContain("Never skip review.");
    }

    [Fact]
    public async Task ListEntries_Default_KeepsTagsInProjection()
    {
        Stub.RouteGet($"/api/projects/{Canned.ProjectId}/entries", """
            [{"id":"12121212-1212-1212-1212-121212121212","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Tagged note","content":"Body with known length.","currentVersion":2,"createdAt":"2026-09-01T09:00:00Z","updatedAt":"2026-09-02T09:00:00Z","tags":["alpha","beta"],"skillFiles":[]}]
            """);

        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var entry = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray().Single()!.AsObject();
        entry["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["alpha", "beta"]);
        entry["contentLength"]!.GetValue<int>().ShouldBe("Body with known length.".Length);
    }

    [Fact]
    public async Task ListEntries_IncludeContent_ReturnsFullBodies()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate",
            ["include_content"] = true
        });

        var text = Canned.ToolResultText(response);
        var first = JsonNode.Parse(text)!.AsArray()[0]!.AsObject();
        first["content"]!.GetValue<string>().ShouldBe("Always run the full test suite.");
        text.ShouldContain("Never skip review.");

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        request.Query.ShouldBe("type=gate");
    }

    [Fact]
    public async Task ListEntries_TagTypeAndQ_ReachUpstreamQueryEscaped()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "note",
            ["tag"] = "deploy",
            ["q"] = "release checklist"
        });

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        var query = ParseQuery(request.Query);
        query["type"].ShouldBe("note");
        query["tag"].ShouldBe("deploy");
        query["q"].ShouldBe("release checklist");
    }

    [Fact]
    public async Task ToolsList_ListEntries_DescriptionPinsMetadataDefault()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["list_entries"]!["description"]!.GetValue<string>().ShouldBe(
            "Lists entries of a project. Returns metadata only by default: id, type, title, tags, createdAt, updatedAt and contentLength in characters — no content. " +
            "Read a single body with read_entry, or pass include_content=true to receive entries with full content. " +
            "Optional filters: type (note, decision, gate, guard, skill, tombstone), tag and q (fulltext). " +
            "All matching entries are returned in one array, most recently updated first; there is no pagination, and a q search is capped at 100 hits.");

        var properties = tools["list_entries"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("tag");
        properties.ShouldContainKey("q");
        properties.ShouldContainKey("include_content");
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
        var confirmation = JsonNode.Parse(text)!.AsObject();
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.EntryId.ToString());
        confirmation["type"]!.GetValue<string>().ShouldBe("gate");
        confirmation["version"]!.GetValue<int>().ShouldBe(1);
        confirmation["bodyChars"]!.GetValue<int>().ShouldBe("Gate content body.".Length);
        text.ShouldNotContain("Gate content body.");
        text.ShouldNotContain("New gate");
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
        var confirmation = JsonNode.Parse(text)!.AsObject();
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.EntryId.ToString());
        confirmation["type"]!.GetValue<string>().ShouldBe("note");
        confirmation["version"]!.GetValue<int>().ShouldBe(3);
        confirmation["bodyChars"]!.GetValue<int>().ShouldBe(Canned.StoredLength(Canned.EntryJson, "content"));
        text.ShouldNotContain("Updated content body.");
        text.ShouldNotContain("Entry One");
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
    public async Task Search_ProjectsHitsToMetadataAndSnippet()
    {
        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "hello",
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var hits = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        hits.Count.ShouldBe(1);

        var hit = hits[0]!.AsObject();
        hit["source"]!.GetValue<string>().ShouldBe("entry");
        hit["id"]!.GetValue<string>().ShouldBe(Canned.EntryId.ToString());
        hit["title"]!.GetValue<string>().ShouldBe("Entry One");
        hit["type"]!.GetValue<string>().ShouldBe("note");
        hit["snippet"]!.GetValue<string>().ShouldBe("hello world snippet");
        hit.ShouldNotContainKey("rank");

        var request = FindRequest("GET", "/api/search");
        var query = ParseQuery(request.Query);
        query["projectId"].ShouldBe(Canned.ProjectId.ToString());
        query["limit"].ShouldBe("20");
    }

    [Fact]
    public async Task Search_Snippet_WindowsAroundWholeQueryMatch()
    {
        Stub.RouteGet("/api/search", """
            {"query":"deploy pipeline","hits":[{"entryId":"33333333-3333-3333-3333-333333333333","title":"Entry One","type":"note","snippet":"alpha intro\nbeta line\ngamma line\ndelta line\nepsilon line\nzeta talks about <mark>deploy pipeline</mark> hooks\neta line\ntheta line\niota line","rank":0.9}]}
            """);

        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "deploy pipeline"
        });

        var hit = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray()[0]!.AsObject();
        hit["snippet"]!.GetValue<string>().ShouldBe(
            "...\ndelta line\nepsilon line\nzeta talks about deploy pipeline hooks\neta line\ntheta line\n...");
        hit["snippet"]!.GetValue<string>().ShouldNotContain("<mark>");
    }

    [Fact]
    public async Task Search_Snippet_FallsBackToFirstTermThatMatches()
    {
        Stub.RouteGet("/api/search", """
            {"query":"retry backoff","hits":[{"entryId":"33333333-3333-3333-3333-333333333333","title":"Entry One","type":"note","snippet":"alpha mentions backoff only\nbeta plain\ngamma plain\ndelta mentions <mark>retry</mark> policy\nepsilon plain\nzeta plain\neta plain\ntheta plain","rank":0.8}]}
            """);

        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "retry backoff"
        });

        var hit = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray()[0]!.AsObject();
        hit["snippet"]!.GetValue<string>().ShouldBe(
            "...\nbeta plain\ngamma plain\ndelta mentions retry policy\nepsilon plain\nzeta plain\n...");
    }

    [Fact]
    public async Task Search_Snippet_NoTermMatch_ClipsFromTop()
    {
        Stub.RouteGet("/api/search", """
            {"query":"quantum","hits":[{"entryId":"33333333-3333-3333-3333-333333333333","title":"Entry One","type":"note","snippet":"line one\nline two\nline three\nline four\nline five\nline six\nline seven","rank":0.7}]}
            """);

        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "quantum"
        });

        var hit = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray()[0]!.AsObject();
        hit["snippet"]!.GetValue<string>().ShouldBe("line one\nline two\nline three\nline four\nline five\n...");
    }

    [Fact]
    public async Task Search_MixedSources_ProjectsPerSourceFieldsWithoutBodies()
    {
        Stub.RouteGet("/api/search", Canned.SearchUnionJson);

        var response = await Driver.CallToolAsync("search", new JsonObject
        {
            ["query"] = "marmot"
        });

        var hits = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        hits.Count.ShouldBe(3);

        var entry = hits[0]!.AsObject();
        entry["source"]!.GetValue<string>().ShouldBe("entry");
        entry["id"]!.GetValue<string>().ShouldBe(Canned.EntryId.ToString());
        entry["title"]!.GetValue<string>().ShouldBe("Entry One");
        entry["type"]!.GetValue<string>().ShouldBe("note");
        entry["snippet"]!.GetValue<string>().ShouldContain("marmot");
        entry.ShouldNotContainKey("rank");
        entry.ShouldNotContainKey("featureSlug");

        var feature = hits[1]!.AsObject();
        feature["source"]!.GetValue<string>().ShouldBe("feature");
        feature["featureSlug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        feature["title"]!.GetValue<string>().ShouldBe("Demo feature");
        feature["snippet"]!.GetValue<string>().ShouldContain("marmot");
        feature.ShouldNotContainKey("id");
        feature.ShouldNotContainKey("rank");

        var artifact = hits[2]!.AsObject();
        artifact["source"]!.GetValue<string>().ShouldBe("artifact");
        artifact["artifactId"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskOneId.ToString());
        artifact["featureSlug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        artifact["kind"]!.GetValue<string>().ShouldBe("task");
        artifact["order"]!.GetValue<int>().ShouldBe(7);
        artifact["label"]!.GetValue<string>().ShouldBe("039 task");
        artifact["snippet"]!.GetValue<string>().ShouldContain("marmot");
        artifact.ShouldNotContainKey("title");
        artifact.ShouldNotContainKey("rank");
    }

    [Fact]
    public async Task ToolsList_Search_DescriptionPinsSourceAndSnippetProjection()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["search"]!["description"]!.GetValue<string>().ShouldBe(
            "Searches entries and planning artifacts by fulltext; semantic mode searches entries only and fails with 'vector search unavailable' when the vector store is down. " +
            "Fulltext covers entries plus planning features and their spec/plan/task/result bodies. " +
            "Each hit carries its source (entry, feature or artifact) with only its id fields, a title or label, and a snippet — a short window of lines around the first query-term match; " +
            "full bodies are not returned, read a full entry with read_entry or an artifact with planning_read_artifact.");
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
    [InlineData("tombstone")]
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
    public async Task DeleteEntry_RemovesANote_AndItLeavesTheListing()
    {
        var deleted = false;
        Stub.Route(
            "GET",
            $"/api/projects/{Canned.ProjectId}/entries",
            _ => deleted
                ? new StubOvermindApi.Response(200, "[]")
                : new StubOvermindApi.Response(200, Canned.NotesJson));
        Stub.Route(
            "DELETE",
            "/api/entries/[^/]+",
            _ =>
            {
                deleted = true;
                return new StubOvermindApi.Response(204, "");
            });

        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["id"]!.GetValue<string>().ShouldBe(Canned.EntryId.ToString());
        echo["type"]!.GetValue<string>().ShouldBe("note");
        echo["deleted"]!.GetValue<bool>().ShouldBeTrue();

        var listing = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });
        Canned.ToolResultText(listing).ShouldNotContain("Note One");
    }

    [Fact]
    public async Task DeleteEntry_Reason_RidesTheUpstreamQueryEscaped()
    {
        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["reason"] = "dead & buried — 100% superseded #graves"
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("DELETE", $"/api/entries/{Canned.EntryId}");
        ParseQuery(request.Query)["reason"].ShouldBe("dead & buried — 100% superseded #graves");
    }

    [Fact]
    public async Task DeleteEntry_BlankReason_LeavesTheQueryOff()
    {
        await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["reason"] = "   "
        });

        FindRequest("DELETE", $"/api/entries/{Canned.EntryId}").Query.ShouldBe("");
    }

    [Fact]
    public async Task DeleteEntry_KitLinkedEntry_RefusedWithKitOwnedEntryVerbatim()
    {
        Stub.RouteGet("/api/kits", Canned.KitsJson);

        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("KitOwnedEntry");
        text.ShouldContain("managed by a skill kit 'core-kit'");
        text.ShouldContain("the kit reimport or the UI owns it");

        Stub.Requests.ShouldNotContain(r => r.Method == "DELETE");
    }

    [Fact]
    public async Task DeleteEntry_SkillKindEntry_RefusedWithoutDeleting()
    {
        Stub.RouteGet($"/api/entries/{Canned.EntryId}", Canned.SkillEntryJson);

        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("SkillEntryDeleteRefused");
        text.ShouldContain("Overmind UI");

        Stub.Requests.ShouldNotContain(r => r.Method == "DELETE");
    }

    [Fact]
    public async Task DeleteEntry_EntryFromAnotherProject_RefusedBeforeAnyDeletion()
    {
        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.SecondProjectId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("does not belong to the resolved project");

        Stub.Requests.ShouldNotContain(r => r.Method == "DELETE");
    }

    [Fact]
    public async Task DeleteEntry_NoExplicitProject_ResolvesThroughTheOnboardCwd()
    {
        await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        JsonNode.Parse(onboard.Body)!["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        FindRequest("DELETE", $"/api/entries/{Canned.EntryId}");
    }

    [Fact]
    public async Task DeleteEntry_ProjectIdEnvOverride_SkipsOnboard()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.ProjectId.ToString()
        });
        await driver.InitializeAsync();

        await driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString()
        });

        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        Stub.Requests.ShouldContain(r => r.Method == "DELETE" && r.Path == $"/api/entries/{Canned.EntryId}");
    }

    [Fact]
    public async Task DeleteEntry_Description_PinsTheTombstoneTruthAndTheGuards()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["delete_entry"]!["description"]!.GetValue<string>();

        description.ShouldContain("permanent");
        description.ShouldContain("version history are gone");
        description.ShouldContain("' (deleted)'");
        description.ShouldContain("provenance");
        description.ShouldContain("reason");
        description.ShouldContain("fenced block");
        description.ShouldContain("no second tombstone");
        description.ShouldContain("type=tombstone");
        description.ShouldNotContain("no tombstones");
        description.ShouldNotContain("no undo");
        description.ShouldContain("KitOwnedEntry");
        description.ShouldContain("the kit reimport or the UI owns it");
        description.ShouldContain("OVERMIND_PROJECT_ID");

        var properties = tools["delete_entry"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("reason");
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
