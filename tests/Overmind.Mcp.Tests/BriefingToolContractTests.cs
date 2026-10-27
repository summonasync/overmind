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

        var briefingRequests = Stub.Requests
            .Where(r => r.Path == $"/api/projects/{Canned.ProjectId}/entries")
            .ToList();
        briefingRequests.Count.ShouldBe(5);
        briefingRequests.Select(r => ParseQuery(r.Query).GetValueOrDefault("type")).ShouldBe(
            ["guard", "gate", "decision", "note", "tombstone"]);
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
        foreach (var section in new[] { "guards", "gates", "decisions", "notes", "tombstones" })
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
            .ShouldBe(["guard", "gate", "decision", "note"]);
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

        Stub.Requests.Count(r => r.Path == $"/api/projects/{Canned.ProjectId}/entries").ShouldBe(5);
    }

    [Fact]
    public async Task ToolsList_OvermindBriefing_DescriptionPinsTheOneToolDecisionAndDefaults()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var tool = tools["overmind_briefing"]!;

        tool["description"]!.GetValue<string>().ShouldBe(
            "ONE call returns the start-of-work snapshot of a project — the guardrail picture an agent absorbs before any work: " +
            "guards and gates with full bodies, decisions and the latest notes as ~200-character snippets (read_entry for depth), " +
            "and the tombstone list of buried entries. " +
            "One consolidated tool by design: four specialized getguards/getgates/getnotes/gettombstones lookups would multiply " +
            "the discovery surface for a single moment-in-time need; list_entries stays the exploration tool, this one is the start ritual. " +
            "notesLimit defaults to 10; includeTombstones=false trims the graves section; categories without entries come back as " +
            "empty arrays, never omitted keys; entries arrive most-recently-updated first. " +
            "Read-only: projectId is resolved from OVERMIND_PROJECT_ID or the working directory when omitted.");

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
