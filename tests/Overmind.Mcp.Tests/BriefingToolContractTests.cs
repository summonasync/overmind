using System.Globalization;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class BriefingToolContractTests : McpSessionTestBase
{
    private const string ThreeNotesJson =
        """
        [
          {"id":"33333333-3333-3333-3333-333333333333","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Fresh note","content":"fresh body","currentVersion":1,"createdAt":"2026-09-10T10:00:00Z","updatedAt":"2026-09-10T10:00:00Z","tags":[],"skillFiles":[]},
          {"id":"34343434-3434-3434-3434-343434343434","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Middle note","content":"middle body","currentVersion":1,"createdAt":"2026-09-05T10:00:00Z","updatedAt":"2026-09-05T10:00:00Z","tags":[],"skillFiles":[]},
          {"id":"35353535-3535-3535-3535-353535353535","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Stale note","content":"stale body","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task Briefing_Defaults_ShapePerCategoryWithFullGuardsAndGates()
    {
        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        briefing["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());

        var guard = briefing["guards"]!.AsArray().Single()!.AsObject();
        guard.Select(field => field.Key).ShouldBe(["id", "title", "content", "updatedAt"]);
        guard["title"]!.GetValue<string>().ShouldBe("Guard One");
        guard["content"]!.GetValue<string>().ShouldBe("Do not touch generated code.");

        var gates = briefing["gates"]!.AsArray();
        gates.Count.ShouldBe(2);
        gates[0]!.AsObject()["content"]!.GetValue<string>().ShouldBe("Always run the full test suite.");
        gates[1]!.AsObject()["content"]!.GetValue<string>().ShouldBe("Never skip review.");

        var decision = briefing["decisions"]!.AsArray().Single()!.AsObject();
        decision.Select(field => field.Key).ShouldBe(["id", "title", "snippet", "updatedAt"]);
        decision["snippet"]!.GetValue<string>().ShouldBe("Use SQLite for storage.");
        decision.ShouldNotContainKey("content");

        var note = briefing["notes"]!.AsArray().Single()!.AsObject();
        note.Select(field => field.Key).ShouldBe(["id", "title", "snippet", "updatedAt"]);
        note["snippet"]!.GetValue<string>().ShouldBe("Some note body.");
        note["updatedAt"]!.GetValue<string>().ShouldBe("2026-09-01T10:00:00Z");

        var grave = briefing["tombstones"]!.AsArray().Single()!.AsObject();
        grave.Select(field => field.Key).ShouldBe(["id", "title", "type", "deletedAt"]);
        grave["title"]!.GetValue<string>().ShouldBe("Decision One (deleted)");
        grave["type"]!.GetValue<string>().ShouldBe("tombstone");
        grave["deletedAt"]!.GetValue<string>().ShouldBe("2026-10-04T12:00:00Z");
        grave.ShouldNotContainKey("content");

        briefing.ShouldContainKey("liftedRecently");
        briefing["liftedRecently"]!.AsArray().Count.ShouldBe(0);

        var briefingRequests = Stub.Requests
            .Where(r => r.Path == $"/api/projects/{Canned.ProjectId}/entries")
            .ToList();
        briefingRequests.Count.ShouldBe(6);
        briefingRequests.Select(r => ParseQuery(r.Query).GetValueOrDefault("type")).ShouldBe(
            ["guard", "gate", "decision", "note", null, "tombstone"]);
        briefingRequests.Select(r => ParseQuery(r.Query).GetValueOrDefault("state")).ShouldBe(
            ["active", "active", "active", null, null, null]);
        briefingRequests.ShouldAllBe(r => r.Method == "GET");
    }

    [Fact]
    public async Task Briefing_DecisionSnippet_FlattensLinesAndClipsAtTwoHundredTextElements()
    {
        RouteDecisionOnly(string.Join('\n', Enumerable.Repeat("🐗🐗🐗 herd line", 100)));

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var snippet = JsonNode.Parse(Canned.ToolResultText(response))!["decisions"]!.AsArray()[0]!
            .AsObject()["snippet"]!.GetValue<string>();
        snippet.ShouldNotContain("\n");
        snippet.ShouldEndWith("…");
        new StringInfo(snippet).LengthInTextElements.ShouldBe(201);
    }

    [Fact]
    public async Task Briefing_DecisionSnippet_ShortMultilineBody_FlattensVerbatim()
    {
        RouteDecisionOnly("# Storage choice\n\nWe picked SQLite over Postgres for the single-file story.");

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var decision = JsonNode.Parse(Canned.ToolResultText(response))!["decisions"]!.AsArray().Single()!.AsObject();
        decision["snippet"]!.GetValue<string>().ShouldBe(
            "# Storage choice We picked SQLite over Postgres for the single-file story.");
    }

    [Fact]
    public async Task Briefing_CarriesLiveSurfaces_WhenTheRosterWatchesPaths()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/lanes",
            """
            {"lanes":[
              {"name":"web-boundary","contour":"web src","serialOrder":1,"priority":0,"liveSurfaces":[{"glob":"web/src/**","owner":"van-darkholme"},{"glob":"luca/umbracolistener/src/**","owner":"billy-herrington"}],"openFeatureCount":1,"holder":null},
              {"name":"backend","contour":null,"serialOrder":null,"priority":null,"openFeatureCount":0,"holder":null}
            ]}
            """);

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var live = briefing["liveSurfaces"]!.AsArray();
        live.Count.ShouldBe(2);
        live[0]!.AsObject().Select(field => field.Key).ShouldBe(["lane", "glob", "owner"]);
        live[0]!.AsObject()["lane"]!.GetValue<string>().ShouldBe("web-boundary");
        live[0]!.AsObject()["glob"]!.GetValue<string>().ShouldBe("web/src/**");
        live[0]!.AsObject()["owner"]!.GetValue<string>().ShouldBe("van-darkholme");
        live[1]!.AsObject()["owner"]!.GetValue<string>().ShouldBe("billy-herrington");
    }

    [Fact]
    public async Task Briefing_OmitsLiveSurfaces_WhenNoLaneWatchesAnything()
    {
        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        briefing.ShouldNotContainKey("liveSurfaces");
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public async Task Briefing_NotesLimit_TakesTheHeadOfTheUpstreamOrder(int limit, int expected)
    {
        Stub.Route("GET", $"/api/projects/{Canned.ProjectId}/entries", req =>
            ParseQuery(req.Query).GetValueOrDefault("type") == "note"
                ? new StubOvermindApi.Response(200, ThreeNotesJson)
                : new StubOvermindApi.Response(200, "[]"));

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["notesLimit"] = limit
        });

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var notes = briefing["notes"]!.AsArray();
        notes.Count.ShouldBe(expected);

        if (expected > 0)
        {
            notes[0]!.AsObject()["title"]!.GetValue<string>().ShouldBe("Fresh note");
            notes[0]!.AsObject()["snippet"]!.GetValue<string>().ShouldBe("fresh body");
        }
    }

    [Fact]
    public async Task Briefing_EmptyProject_ReturnsEveryCategoryAsAnEmptyArray()
    {
        Stub.Route("GET", $"/api/projects/{Canned.ProjectId}/entries", _ => new StubOvermindApi.Response(200, "[]"));

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        foreach (var section in new[] { "guards", "gates", "decisions", "notes", "tombstones", "liftedRecently" })
        {
            briefing.ContainsKey(section).ShouldBeTrue($"section '{section}' must never be omitted");
            briefing[section]!.AsArray().Count.ShouldBe(0);
        }
    }

    [Fact]
    public async Task Briefing_CarriesTheStaticMemoryLayerTeachingSection()
    {
        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();

        var memoryLayer = JsonNode.Parse(Canned.ToolResultText(response))!["memoryLayer"]!.AsObject();
        var note = memoryLayer["note"]!.GetValue<string>();
        note.ShouldContain("Memory layer (Grimoire)");
        note.ShouldContain("memory_brief / memory_recall");
        note.ShouldContain("teaching, not state");
        memoryLayer["boardToMemory"]!.GetValue<string>().ShouldContain("pointer: grimoire://memory/<id>");
        memoryLayer["memoryToBoard"]!.GetValue<string>().ShouldContain("overmind://entry/<uuid>");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("grimoire"));
    }

    [Fact]
    public async Task Briefing_IncludeTombstonesFalse_TrimsTheSectionAndSkipsTheFetch()
    {
        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["includeTombstones"] = false
        });

        response["result"]!["isError"].ShouldBeNull();

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        briefing.ContainsKey("tombstones").ShouldBeFalse();
        briefing["guards"]!.AsArray().Count.ShouldBe(1);

        Stub.Requests
            .Where(r => r.Path == $"/api/projects/{Canned.ProjectId}/entries")
            .Select(r => ParseQuery(r.Query).GetValueOrDefault("type"))
            .ShouldBe(["guard", "gate", "decision", "note", null]);
    }

    [Fact]
    public async Task Briefing_NegativeNotesLimit_IsToolErrorWithoutAnyFetch()
    {
        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["notesLimit"] = -1
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("notesLimit");
        text.ShouldContain("zero or a positive");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/entries"));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Briefing_NoExplicitProject_ResolvesCreateFreeThroughTheOnboardCwd()
    {
        await Driver.CallToolAsync("overmind_briefing", new JsonObject());

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();

        Stub.Requests.Count(r => r.Path == $"/api/projects/{Canned.ProjectId}/entries").ShouldBe(6);
    }

    [Fact]
    public async Task ToolsList_OvermindBriefing_DescriptionPinsTheOneToolDecisionAndDefaults()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var tool = tools["overmind_briefing"]!;

        tool["description"]!.GetValue<string>().ShouldBe(
            "ONE call returns the start-of-work snapshot of a project — guards and gates with full bodies, " +
            "decisions and the latest notes as snippets, tombstones, and the lifted-recently tail. " +
            "The read ritual before any work; list_entries stays the exploration tool, briefing the snapshot. Read-only.");

        var section = ContractsDoc.Section("overmind_briefing");
        section.ShouldContain("guards and gates with full bodies; decisions and the latest notes as ~200-char snippets");
        section.ShouldContain("Guards, gates and decisions render active entries only");
        section.ShouldContain("decisions carrying enforces=true additionally appear in the guards section marked decision·enforces");
        section.ShouldContain("entries lifted or superseded within the last 14 days, max 5 rows, newest first");
        section.ShouldContain("four specialized getguards/getgates/ getnotes/gettombstones lookups would multiply the discovery surface");
        section.ShouldContain("list_entries stays the exploration tool, this is the start ritual");
        section.ShouldContain("notesLimit defaults to 10; includeTombstones=false trims the graves section");
        section.ShouldContain("categories without entries come back as empty arrays, never omitted keys");
        section.ShouldContain("entries arrive most-recently-updated first");
        section.ShouldContain("a liveSurfaces section lists them so the start-of-work picture warns before any watched path is touched");

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("notesLimit");
        properties.ShouldContainKey("includeTombstones");
        tool["inputSchema"]!["required"].ShouldBeNull();
    }

    private void RouteDecisionOnly(string content)
    {
        Stub.Route("GET", $"/api/projects/{Canned.ProjectId}/entries", req =>
            ParseQuery(req.Query).GetValueOrDefault("type") == "decision"
                ? new StubOvermindApi.Response(200, DecisionJson(content))
                : new StubOvermindApi.Response(200, "[]"));
    }

    [Fact]
    public async Task Briefing_EnforcesDecision_LandsInTheGuardsSectionMarkedAndStaysInDecisions()
    {
        var decision = new JsonObject
        {
            ["id"] = "dddddddd-dddd-dddd-dddd-dddddddddddd",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision",
            ["title"] = "Release freeze",
            ["content"] = "No releases during the sprint.",
            ["currentVersion"] = 1,
            ["createdAt"] = "2026-09-01T10:00:00Z",
            ["updatedAt"] = "2026-09-01T10:00:00Z",
            ["tags"] = new JsonArray(),
            ["skillFiles"] = new JsonArray(),
            ["state"] = "active",
            ["stateAt"] = null,
            ["stateBy"] = null,
            ["stateReason"] = null,
            ["stateProof"] = null,
            ["supersededById"] = null,
            ["enforces"] = true
        };

        Stub.Route(
            "GET",
            $"/api/projects/{Canned.ProjectId}/entries",
            req => ParseQuery(req.Query).GetValueOrDefault("type") switch
            {
                "decision" => new StubOvermindApi.Response(200, new JsonArray(decision.DeepClone()).ToJsonString()),
                _ => new StubOvermindApi.Response(200, "[]")
            });

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var briefing = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();

        var guards = briefing["guards"]!.AsArray();
        guards.Count.ShouldBe(1);
        var marked = guards[0]!.AsObject();
        marked.Select(field => field.Key).ShouldBe(["id", "title", "content", "updatedAt", "kind"]);
        marked["kind"]!.GetValue<string>().ShouldBe("decision·enforces");
        marked["title"]!.GetValue<string>().ShouldBe("Release freeze");

        var decisions = briefing["decisions"]!.AsArray();
        decisions.Count.ShouldBe(1);
        decisions[0]!["title"]!.GetValue<string>().ShouldBe("Release freeze");
    }

    [Fact]
    public async Task Briefing_LiftedRecently_ListsRecentLiftsNewestFirstWithSuccessors()
    {
        var freeze = Guid.Parse("5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a");
        var storage = Guid.Parse("5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b");
        var postgres = Guid.Parse("5c5c5c5c-5c5c-5c5c-5c5c-5c5c5c5c5c5c");
        var ancient = Guid.Parse("5d5d5d5d-5d5d-5d5d-5d5d-5d5d5d5d5d5d");
        var now = DateTimeOffset.UtcNow;

        RouteLifecycleEntries(
            RetiredEntry(freeze, "gate", "Release freeze", "lifted", now.AddDays(-2), "The release shipped.", null),
            RetiredEntry(storage, "decision", "Storage choice", "superseded", now.AddDays(-1), "Postgres took over.", postgres),
            RetiredEntry(postgres, "decision", "Postgres default", "active", now.AddDays(-1), string.Empty, null),
            RetiredEntry(ancient, "guard", "Ancient guard", "lifted", now.AddDays(-20), "Long gone.", null));

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var tail = JsonNode.Parse(Canned.ToolResultText(response))!["liftedRecently"]!.AsArray();
        tail.Count.ShouldBe(2);

        var newest = tail[0]!.AsObject();
        newest["id"]!.GetValue<string>().ShouldBe(storage.ToString());
        newest["title"]!.GetValue<string>().ShouldBe("Storage choice");
        newest["state"]!.GetValue<string>().ShouldBe("superseded");
        newest["stateAt"]!.GetValue<string>().ShouldBe(now.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        newest["reason"]!.GetValue<string>().ShouldBe("Postgres took over.");
        newest["successor"]!.GetValue<string>().ShouldBe("Postgres default");

        var lifted = tail[1]!.AsObject();
        lifted["state"]!.GetValue<string>().ShouldBe("lifted");
        lifted.ShouldNotContainKey("successor");
    }

    [Fact]
    public async Task Briefing_LiftedRecently_CapsAtFiveRowsNewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = Enumerable.Range(1, 6)
            .Select(i => RetiredEntry(
                Guid.Parse($"6{i:D7}-0000-0000-0000-000000000000"),
                "decision",
                $"Lifted number {i}",
                "lifted",
                now.AddHours(-i),
                $"Reason {i}.",
                null))
            .ToArray();

        RouteLifecycleEntries(entries);

        var response = await Driver.CallToolAsync("overmind_briefing", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var tail = JsonNode.Parse(Canned.ToolResultText(response))!["liftedRecently"]!.AsArray();
        tail.Count.ShouldBe(5);
        tail[0]!["title"]!.GetValue<string>().ShouldBe("Lifted number 1");
        tail[4]!["title"]!.GetValue<string>().ShouldBe("Lifted number 5");
    }

    private void RouteLifecycleEntries(params JsonObject[] entries)
    {
        var payload = new JsonArray();
        foreach (var entry in entries)
        {
            payload.Add(entry.DeepClone());
        }

        Stub.Route(
            "GET",
            $"/api/projects/{Canned.ProjectId}/entries",
            req => ParseQuery(req.Query).ContainsKey("type")
                ? new StubOvermindApi.Response(200, "[]")
                : new StubOvermindApi.Response(200, payload.ToJsonString()));
    }

    private static JsonObject RetiredEntry(
        Guid id,
        string type,
        string title,
        string state,
        DateTimeOffset stateAt,
        string reason,
        Guid? supersededById)
    {
        var stamp = stateAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return new JsonObject
        {
            ["id"] = id.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = type,
            ["title"] = title,
            ["content"] = "body",
            ["currentVersion"] = 1,
            ["createdAt"] = stamp,
            ["updatedAt"] = stamp,
            ["tags"] = new JsonArray(),
            ["skillFiles"] = new JsonArray(),
            ["state"] = state,
            ["stateAt"] = stamp,
            ["stateBy"] = "Dan (qwen)",
            ["stateReason"] = reason,
            ["stateProof"] = null,
            ["supersededById"] = supersededById?.ToString(),
            ["enforces"] = false
        };
    }

    private static string DecisionJson(string content)
    {
        var entry = new JsonObject
        {
            ["id"] = "dddddddd-dddd-dddd-dddd-dddddddddddd",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "decision",
            ["title"] = "Decision One",
            ["content"] = content,
            ["currentVersion"] = 1,
            ["createdAt"] = "2026-09-01T10:00:00Z",
            ["updatedAt"] = "2026-09-01T10:00:00Z",
            ["tags"] = new JsonArray(),
            ["skillFiles"] = new JsonArray()
        };

        return new JsonArray(entry).ToJsonString();
    }
}
