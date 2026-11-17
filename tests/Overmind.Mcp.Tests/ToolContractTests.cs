using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.RouteGet($"/api/entries/{Canned.DecisionEntryId}", Canned.DecisionEntryJson);
        stub.Route("PUT", $"/api/entries/{Canned.DecisionEntryId}", 200, Canned.DecisionEntryJson);
    }

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
    public async Task ListEntries_IdeaType_PassesThroughAndProjectsLikeAnyEntry()
    {
        Stub.RouteGet($"/api/projects/{Canned.ProjectId}/entries", """
            [{"id":"14141414-1414-1414-1414-141414141414","projectId":"11111111-1111-1111-1111-111111111111","type":"idea","title":"Vector cache","content":"Unformed product thinking.","currentVersion":0,"createdAt":"2026-10-13T09:00:00Z","updatedAt":"2026-10-13T09:00:00Z","tags":["intake"],"skillFiles":[]}]
            """);

        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "idea"
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query)["type"].ShouldBe("idea");

        var entry = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray().Single()!.AsObject();
        entry["type"]!.GetValue<string>().ShouldBe("idea");
        entry["title"]!.GetValue<string>().ShouldBe("Vector cache");
        entry["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["intake"]);
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
    public async Task ListEntries_FieldsSlice_ReturnsIdPlusRequestedKeysInRequestOrder()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate",
            ["fields"] = "title,updatedAt"
        });

        response["result"]!["isError"].ShouldBeNull();

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row!.AsObject().Select(pair => pair.Key).ShouldBe(["id", "title", "updatedAt"]);
        }

        var first = rows[0]!.AsObject();
        first["id"]!.GetValue<string>().ShouldBe("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        first["title"]!.GetValue<string>().ShouldBe("Gate One");
        first["updatedAt"]!.GetValue<string>().ShouldBe("2026-09-01T10:00:00Z");
    }

    [Fact]
    public async Task ListEntries_FieldsComposeWithTypeFilter()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "tombstone",
            ["fields"] = "type,tags"
        });

        response["result"]!["isError"].ShouldBeNull();

        ParseQuery(FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries").Query)["type"].ShouldBe("tombstone");

        var row = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray().Single()!.AsObject();
        row.Select(pair => pair.Key).ShouldBe(["id", "type", "tags"]);
        row["type"]!.GetValue<string>().ShouldBe("tombstone");
        row["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["buried"]);
    }

    [Fact]
    public async Task ListEntries_FieldsWithIncludeContent_SlicesTheRawRows()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "gate",
            ["include_content"] = true,
            ["fields"] = "title,content"
        });

        response["result"]!["isError"].ShouldBeNull();

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row!.AsObject().Select(pair => pair.Key).ShouldBe(["id", "title", "content"]);
        }

        rows[0]!["content"]!.GetValue<string>().ShouldBe("Always run the full test suite.");
    }

    [Fact]
    public async Task ListEntries_UnknownField_RejectedNamingTheAllowedSet_BeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "title,banana"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("banana");
        text.ShouldContain("Allowed fields:");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/entries", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListEntries_EmptyFieldsList_RejectedAsACallerBug_BeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = ",,,"
        });

        ToolErrorText(response).ShouldContain("projection of nothing");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/entries", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListEntries_AuditTypeFilter_IsAcceptedAndProxied()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "audit"
        });

        response["result"]!["isError"].ShouldBeNull();
        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query)["type"].ShouldBe("audit");
    }

    [Fact]
    public async Task ListEntries_StateFilter_RidesTheUpstreamQuery()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision",
            ["state"] = "all"
        });

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        var query = ParseQuery(request.Query);
        query["state"].ShouldBe("all");
        query["type"].ShouldBe("decision");
    }

    [Fact]
    public async Task ListEntries_LiftedState_IsPassedThroughVerbatim()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["state"] = "lifted"
        });

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query)["state"].ShouldBe("lifted");
    }

    [Fact]
    public async Task ListEntries_NoState_LeavesTheFilterOffTheQuery()
    {
        await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision"
        });

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query).ShouldNotContainKey("state");
    }

    [Fact]
    public async Task ToolsList_ListEntries_DescriptionPinsMetadataDefault()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["list_entries"]!["description"]!.GetValue<string>().ShouldBe(
            "Lists a project's entries as metadata rows — the exploration door (bodies via include_content, " +
            "one full entry via read_entry, the start-of-work snapshot via overmind_briefing). " +
            "Newest-updated first; no pagination; q caps at 100 hits.");

        var section = ContractsDoc.Section("list_entries");
        tools["list_entries"]!["description"]!.GetValue<string>().ShouldContain("q caps at 100 hits");
        tools["list_entries"]!["inputSchema"]!["properties"]!["state"]!["description"]!.GetValue<string>()
            .ShouldContain("all | active | lifted | superseded");
        section.ShouldContain("id, type, title, tags, createdAt, updatedAt, contentLength");
        section.ShouldContain("Bodies arrive only with include_content=true or per-entry via read_entry");
        section.ShouldContain("the server applies its own default — active for the decision/gate/guard trio");
        section.ShouldContain("each row collapses to {id, ...requested} in request order");
        section.ShouldContain("unknown keys are rejected before the call naming the allowed set");
        section.ShouldContain("most recently updated first — there is no pagination");
        section.ShouldContain("An unknown type value fails naming the expected set");
        section.ShouldContain("note, decision, gate, guard, skill, idea, audit, tombstone");

        var properties = tools["list_entries"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("tag");
        properties.ShouldContainKey("q");
        properties.ShouldContainKey("state");
        properties.ShouldContainKey("since");
        properties.ShouldContainKey("until");
        properties.ShouldContainKey("author");
        properties.ShouldContainKey("include_content");
        properties.ShouldContainKey("fields");
    }

    [Fact]
    public async Task ToolsList_AppendDirective_DescriptionStatesThe2000Cap()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var description = tools["planning_append_directive"]!["description"]!.GetValue<string>();
        description.ShouldContain("the one write that crosses the claim lock");
        description.ShouldNotContain("capped at 500 chars");

        ContractsDoc.Section("planning_append_directive")
            .ShouldContain("text is trimmed, capped at 2000 chars enforced adapter-side");

        var textProperty = tools["planning_append_directive"]!["inputSchema"]!["properties"]!["text"]!;
        var parameterDescription = textProperty["description"]!.GetValue<string>();
        parameterDescription.ShouldContain("max 2000 chars");
        parameterDescription.ShouldNotContain("max 500 chars");
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
        confirmation.ShouldNotContainKey("hint");
        text.ShouldNotContain("Gate content body.");
        text.ShouldNotContain("New gate");
    }

    [Fact]
    public async Task WriteEntry_Decision_AppendsTheMemoryMirroringHint()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision",
            ["title"] = "Storage choice",
            ["content"] = "We picked SQLite for storage."
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation["type"]!.GetValue<string>().ShouldBe("decision");
        var hint = confirmation["hint"]!.GetValue<string>();
        hint.ShouldContain("grimoire memory_remember");
        hint.ShouldContain("'pointer: grimoire://memory/<id>'");
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
        confirmation.ShouldNotContainKey("hint");
        text.ShouldNotContain("Updated content body.");
        text.ShouldNotContain("Entry One");
    }

    [Fact]
    public async Task UpdateEntry_Decision_AppendsTheMemoryMirroringHint()
    {
        var response = await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = Canned.DecisionEntryId.ToString(),
            ["content"] = "Revised: SQLite stays, WAL mode on."
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation["type"]!.GetValue<string>().ShouldBe("decision");
        var hint = confirmation["hint"]!.GetValue<string>();
        hint.ShouldContain("grimoire memory_remember");
        hint.ShouldContain("'pointer: grimoire://memory/<id>'");

        var put = FindRequest("PUT", $"/api/entries/{Canned.DecisionEntryId}");
        put.Body.ShouldContain("Revised: SQLite stays, WAL mode on.");
    }

    [Fact]
    public async Task WriteEntry_EnforcesFlag_RidesTheCreateBody()
    {
        await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision",
            ["title"] = "Release freeze",
            ["content"] = "No releases during the sprint.",
            ["enforces"] = true
        });

        var body = JsonNode.Parse(FindRequest("POST", "/api/entries").Body)!;
        body["enforces"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task WriteEntry_EnforcesOnNote_ServerRefusalSurfacesReadable()
    {
        Stub.Route("POST", "/api/entries", 400,
            """{"title":"Validation Error","errors":{"enforces":["Only decision entries can carry the enforces flag."]}}""");

        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "note",
            ["title"] = "Plain note",
            ["content"] = "body",
            ["enforces"] = true
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("Only decision entries can carry the enforces flag");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateEntry_ExplicitEnforces_RidesThePutBody()
    {
        await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = Canned.DecisionEntryId.ToString(),
            ["content"] = "Revised, still standing.",
            ["enforces"] = true
        });

        var body = JsonNode.Parse(FindRequest("PUT", $"/api/entries/{Canned.DecisionEntryId}").Body)!;
        body["enforces"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task LiftEntry_PostsTheLifecycleDoorAndEchoesTheRetiredEntry()
    {
        var successor = Guid.Parse("7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e");
        Stub.Route(
            "POST",
            $"/api/entries/{Canned.DecisionEntryId}/lift",
            200,
            LifecycleEntryJson(Canned.DecisionEntryId, "decision", "superseded", successor.ToString(), 3));

        var response = await Driver.CallToolAsync("lift_entry", new JsonObject
        {
            ["entryId"] = Canned.DecisionEntryId.ToString(),
            ["reason"] = "Postgres took over the storage story.",
            ["proof"] = "notification 53ed854c",
            ["supersededById"] = successor.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();

        var body = JsonNode.Parse(FindRequest("POST", $"/api/entries/{Canned.DecisionEntryId}/lift").Body)!;
        body["reason"]!.GetValue<string>().ShouldBe("Postgres took over the storage story.");
        body["proof"]!.GetValue<string>().ShouldBe("notification 53ed854c");
        body["supersededById"]!.GetValue<Guid>().ShouldBe(successor);

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["id"]!.GetValue<string>().ShouldBe(Canned.DecisionEntryId.ToString());
        echo["type"]!.GetValue<string>().ShouldBe("decision");
        echo["state"]!.GetValue<string>().ShouldBe("superseded");
        echo["supersededById"]!.GetValue<string>().ShouldBe(successor.ToString());
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo.ShouldNotContainKey("content");
    }

    [Fact]
    public async Task LiftEntry_MinimalCall_PostsNullOptionals_AndOmitsTheSuccessorFromTheEcho()
    {
        Stub.Route(
            "POST",
            $"/api/entries/{Canned.DecisionEntryId}/lift",
            200,
            LifecycleEntryJson(Canned.DecisionEntryId, "decision", "lifted", null, 2));

        var response = await Driver.CallToolAsync("lift_entry", new JsonObject
        {
            ["entryId"] = Canned.DecisionEntryId.ToString(),
            ["reason"] = "The freeze is over, the release is out."
        });

        response["result"]!["isError"].ShouldBeNull();

        var raw = FindRequest("POST", $"/api/entries/{Canned.DecisionEntryId}/lift").Body;
        raw.ShouldContain("\"proof\":null");
        raw.ShouldContain("\"supersededById\":null");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["state"]!.GetValue<string>().ShouldBe("lifted");
        echo.ShouldNotContainKey("supersededById");
    }

    [Fact]
    public async Task LiftEntry_ServerRefusal_SurfacesReadable()
    {
        Stub.Route(
            "POST",
            $"/api/entries/{Canned.DecisionEntryId}/lift",
            400,
            """{"title":"Bad Lift","detail":"An entry cannot supersede itself.","status":400}""");

        var response = await Driver.CallToolAsync("lift_entry", new JsonObject
        {
            ["entryId"] = Canned.DecisionEntryId.ToString(),
            ["reason"] = "rotated",
            ["supersededById"] = Canned.DecisionEntryId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("cannot supersede itself");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendEntry_PostsTheFollowUpAndEchoesTheEntry()
    {
        Stub.Route(
            "POST",
            $"/api/entries/{Canned.EntryId}/append",
            200,
            LifecycleEntryJson(Canned.EntryId, "note", "active", null, 4));

        var response = await Driver.CallToolAsync("append_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["text"] = "Server 502s were observed again after the rollback."
        });

        response["result"]!["isError"].ShouldBeNull();

        var body = JsonNode.Parse(FindRequest("POST", $"/api/entries/{Canned.EntryId}/append").Body)!;
        body["text"]!.GetValue<string>().ShouldBe("Server 502s were observed again after the rollback.");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["state"]!.GetValue<string>().ShouldBe("active");
        echo["version"]!.GetValue<int>().ShouldBe(4);
        echo.ShouldNotContainKey("supersededById");
    }

    [Fact]
    public async Task AppendEntry_OverCap_RefusedBeforeAnyPost()
    {
        var response = await Driver.CallToolAsync("append_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["text"] = new string('x', 2001)
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("text is 2001 chars; cap is 2000; cut 1");

        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/append"));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendEntry_SkillEntry_RefusedWithoutPosting()
    {
        Stub.RouteGet($"/api/entries/{Canned.EntryId}", Canned.SkillEntryJson);

        var response = await Driver.CallToolAsync("append_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["text"] = "Follow-up that must not land in a skill body."
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("SkillEntryAppendRefused");
        text.ShouldContain("Overmind UI");

        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/append"));
    }

    [Fact]
    public async Task LiftEntry_Description_PinsTheLifecycleTruth()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["lift_entry"]!["description"]!.GetValue<string>();

        description.ShouldContain("state moves to lifted");
        description.ShouldContain("or superseded when supersededById names the replacement");
        description.ShouldContain("body and title stay untouched");
        description.ShouldContain("Skills and tombstones refuse");

        var section = ContractsDoc.Section("lift_entry");
        section.ShouldContain("the reason, optional proof, actor and date are stamped on the entry as provenance");
        section.ShouldContain("surfaces in the briefing's liftedRecently tail");
        section.ShouldContain("re-lift is allowed, last write wins");

        var properties = tools["lift_entry"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("entryId");
        properties.ShouldContainKey("reason");
        properties.ShouldContainKey("proof");
        properties.ShouldContainKey("supersededById");
    }

    [Fact]
    public async Task AppendEntry_Description_PinsTheVersionMachineryAndTheCap()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["append_entry"]!["description"]!.GetValue<string>();

        description.ShouldContain("Appends a dated '## Follow-up' block");
        description.ShouldContain("through the version machinery");
        description.ShouldContain("capped at 2000 chars");

        var section = ContractsDoc.Section("append_entry");
        section.ShouldContain("One dated ## Follow-up yyyy-MM-dd block lands at the content tail");
        section.ShouldContain("the version bumps");
        section.ShouldContain("stays readable via the versions endpoint");

        var properties = tools["append_entry"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("entryId");
        properties.ShouldContainKey("text");
    }

    private static string LifecycleEntryJson(Guid id, string type, string state, string? supersededById, int version) =>
        $$"""
        {"id":"{{id}}","projectId":"{{Canned.ProjectId}}","type":"{{type}}","title":"Storage choice","content":"We picked SQLite for storage.","currentVersion":{{version}},"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-11-12T09:00:00Z","tags":[],"skillFiles":[],"state":"{{state}}","stateAt":"2026-11-12T09:00:00Z","stateBy":"Dan (qwen)","stateReason":"Replaced by the Postgres decision.","stateProof":null,"supersededById":{{FormatId(supersededById)}},"enforces":false}
        """;

    private static string FormatId(string? id) =>
        id is null ? "null" : $"\"{id}\"";

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
            "Fulltext search over entries plus planning features and artifact bodies — the cross-surface door " +
            "(list_entries explores entries only). Hits carry ids and a snippet, never full bodies. " +
            "since/until/author narrow to entries; semantic mode covers entries only.");

        var section = ContractsDoc.Section("search");
        section.ShouldContain("Semantic mode searches entries only and fails with vector search unavailable");
        section.ShouldContain("since inclusive, until exclusive");
        section.ShouldContain("author matches the exact alias case-insensitively");
        section.ShouldContain("setting any of them drops feature and artifact hits");
        section.ShouldContain("Each hit carries its source (entry, feature or artifact) with only its id fields");
        section.ShouldContain("a short window of lines around the first query-term match");
        section.ShouldContain("read_entry or planning_read_artifact for depth");
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
    [InlineData("idea")]
    [InlineData("audit")]
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

    [Theory]
    [InlineData("banana")]
    [InlineData("featureRequest")]
    public async Task WriteEntry_InvalidType_ReturnsFriendlyToolErrorAndProcessSurvives(string type)
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = type,
            ["title"] = "Nope",
            ["content"] = "body"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain($"Invalid entry type '{type}'");
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
        var onboardBody = JsonNode.Parse(onboard.Body)!;
        onboardBody["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        onboardBody["create"].ShouldBeNull();
        FindRequest("DELETE", $"/api/entries/{Canned.EntryId}");
    }

    [Fact]
    public async Task WriteEntry_NoExplicitProject_ResolvesThroughTheOnboardCwd()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["type"] = "note",
            ["title"] = "Resolved note",
            ["content"] = "body"
        });

        response["result"]!["isError"].ShouldBeNull();

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var onboardBody = JsonNode.Parse(onboard.Body)!;
        onboardBody["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        onboardBody["create"]!.GetValue<bool>().ShouldBeFalse();

        var entry = FindRequest("POST", "/api/entries");
        JsonNode.Parse(entry.Body)!["projectId"]!.GetValue<Guid>().ShouldBe(Canned.ProjectId);
    }

    [Fact]
    public async Task WriteEntry_UnregisteredCwd_ReturnsGuidanceWithoutPostingOrCreating()
    {
        Stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["type"] = "note",
            ["title"] = "Orphan note",
            ["content"] = "body"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("Could not resolve the project for the entry");
        text.ShouldContain("Pass projectId explicitly");

        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/entries");
        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        JsonNode.Parse(onboard.Body)!["create"]!.GetValue<bool>().ShouldBeFalse();
        Driver.HasExited.ShouldBeFalse();
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

        description.ShouldContain("Hard-deletes an entry — row and version history gone");
        description.ShouldContain("births a tombstone in its place carrying title, type, date, provenance and final content");
        description.ShouldContain("Kit- and skill-owned entries are refused");
        description.ShouldContain("to retire a standing rule use lift_entry");

        var section = ContractsDoc.Section("delete_entry");
        section.ShouldContain("Deletion is permanent");
        section.ShouldContain("the row and its version history are gone");
        section.ShouldContain("' (deleted)' appended");
        section.ShouldContain("the optional reason, and the final content preserved in a fenced block");
        section.ShouldContain("no second tombstone");
        section.ShouldContain("list_entries with type=tombstone");
        section.ShouldContain("KitOwnedEntry");
        section.ShouldContain("the kit reimport or the UI owns it");
        // 234: delete_entry is the entry door whose resolution DOES create a missing project row;
        // the boilerplate "resolved from OVERMIND_PROJECT_ID, never auto-creates" it used to carry is gone.
        section.ShouldContain("project resolution here creates the project row when missing");

        foreach (var wording in new[] { "no tombstones", "no undo" })
        {
            description.ShouldNotContain(wording);
            section.ShouldNotContain(wording);
        }

        var properties = tools["delete_entry"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("reason");
    }

    [Fact]
    public async Task AckEntry_Description_PinsThePureReadsAndTheOwnerContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["ack_entry"]!["description"]!.GetValue<string>();

        description.ShouldContain("readBy row");
        description.ShouldContain("the body, version and UpdatedAt are never touched");
        description.ShouldContain("reads stay pure");
        description.ShouldContain("idempotent no-op");

        var section = ContractsDoc.Section("ack_entry");
        section.ShouldContain("The entry body, its version and UpdatedAt are never touched");
        section.ShouldContain("owner-only body writes are unchanged");
        section.ShouldContain("Acking again with the same identity is an idempotent no-op");
        // The "never auto-creates a project row" half of the old prose is a behavior contract and stays
        // pinned as behavior: EntryAckToolContractTests.MissingProjectId_ResolvesThroughTheOnboardCwd.

        var properties = tools["ack_entry"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("entryId");
        properties.ShouldContainKey("projectId");
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

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
