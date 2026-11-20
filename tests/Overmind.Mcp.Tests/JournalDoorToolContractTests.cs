using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class JournalDoorToolContractTests : McpSessionTestBase
{
    private static readonly Guid JournalEntryId = Guid.Parse("5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e");

    private const string JournalEntryJson =
        """
        {"id":"5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e","projectId":"11111111-1111-1111-1111-111111111111","type":"journal","title":"Daily Note","content":"moran thoughts","currentVersion":1,"createdAt":"2026-11-17T10:00:00Z","updatedAt":"2026-11-17T10:00:00Z","tags":[],"skillFiles":[]}
        """;

    private const string JournalListingJson =
        """
        [{"id":"5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e","type":"journal","title":"Daily Note","content":"moran thoughts","tags":[],"createdAt":"2026-11-17T10:00:00Z","updatedAt":"2026-11-17T10:00:00Z"}]
        """;

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.RouteGet($"/api/entries/{JournalEntryId}", JournalEntryJson);
        stub.Route("GET", $"/api/projects/{Canned.ProjectId}/entries", req =>
            ParseQuery(req.Query).GetValueOrDefault("type") == "journal"
                ? new StubOvermindApi.Response(200, JournalListingJson)
                : new StubOvermindApi.Response(200, "[]"));
        stub.Route("PUT", $"/api/agent/entries/{JournalEntryId}", 200, JournalEntryJson);

        // Doors a non-journal entry still walks through: the agent twins added for append and lift.
        stub.Route("POST", $"/api/agent/entries/{Canned.EntryId}/append", 200, Canned.EntryJson);
        stub.Route("POST", $"/api/agent/entries/{Canned.EntryId}/lift", 200, Canned.EntryJson);
        stub.Route("POST", $"/api/agent/entries/{JournalEntryId}/append", 200, JournalEntryJson);
        stub.Route("POST", $"/api/agent/entries/{JournalEntryId}/lift", 200, JournalEntryJson);
        stub.Route("DELETE", $"/api/agent/entries/{JournalEntryId}", 200, JournalEntryJson);
    }

    [Fact]
    public async Task WriteEntry_JournalType_RefusesBeforePosting()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "journal",
            ["title"] = "Daily Note",
            ["content"] = "moran thoughts"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("JournalEntryWriteRefused");
        text.ShouldContain("note, decision, gate, guard, idea, audit, tombstone");

        // The handshake's own POST /api/agent/sessions is not an entry write: the bar here is that
        // the adapter never builds or sends a request for the entry it is about to refuse.
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/entries", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteEntry_NoteType_GoesThroughTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("write_entry", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "note",
            ["title"] = "Note One",
            ["content"] = "body"
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("POST", "/api/agent/entries");
        request.Path.ShouldBe("/api/agent/entries");
        JsonNode.Parse(request.Body)!["type"]!.GetValue<string>().ShouldBe("note");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/entries");
    }

    [Fact]
    public async Task UpdateEntry_NoteType_GoesThroughTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["content"] = "changed"
        });

        response["result"]!["isError"].ShouldBeNull();

        FindRequest("PUT", $"/api/agent/entries/{Canned.EntryId}");
        Stub.Requests.ShouldNotContain(r => r.Method == "PUT" && r.Path == $"/api/entries/{Canned.EntryId}");
    }

    [Fact]
    public async Task UpdateEntry_JournalEntry_RefusesBeforePutting()
    {
        var response = await Driver.CallToolAsync("update_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["content"] = "hijacked"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("JournalEntryWriteRefused");
        Stub.Requests.ShouldNotContain(r => r.Method == "PUT");
    }

    [Fact]
    public async Task ListEntries_JournalType_IsReadableFromAnAgentSession()
    {
        var response = await Driver.CallToolAsync("list_entries", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["type"] = "journal"
        });

        var result = response["result"]!;
        result["isError"].ShouldBeNull($"list_entries(type=journal) failed: {result.ToJsonString()}");
        Canned.ToolResultText(response).ShouldContain("Daily Note");

        var request = FindRequest("GET", $"/api/projects/{Canned.ProjectId}/entries");
        ParseQuery(request.Query)["type"].ShouldBe("journal");
    }

    [Fact]
    public async Task ReadEntry_JournalEntry_IsReadable()
    {
        var response = await Driver.CallToolAsync("read_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldContain("Daily Note");
    }

    [Fact]
    public async Task AppendEntry_JournalEntry_RefusesBeforeAnyMutationRequest()
    {
        var response = await Driver.CallToolAsync("append_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["text"] = "agent slipped a line into the owner's note"
        });

        ToolErrorText(response).ShouldContain("JournalEntryWriteRefused");

        // The type is learned from a read — reads stay open on journal — so the bar is that no entry mutation is
        // sent. The handshake's own POSTs (session registration, onboard) are not mutations and stay out of scope.
        Stub.Requests.ShouldNotContain(r => IsEntryMutation(r));
        Stub.Requests.ShouldContain(r => r.Method == "GET" && r.Path == $"/api/entries/{JournalEntryId}");
    }

    [Fact]
    public async Task LiftEntry_JournalEntry_RefusesBeforeAnyMutationRequest()
    {
        var response = await Driver.CallToolAsync("lift_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["reason"] = "agent retired the owner's note"
        });

        ToolErrorText(response).ShouldContain("JournalEntryWriteRefused");
        Stub.Requests.ShouldNotContain(r => IsEntryMutation(r));
    }

    [Fact]
    public async Task DeleteEntry_JournalEntry_RefusesBeforeAnyMutationRequest()
    {
        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["reason"] = "agent buried the owner's note"
        });

        ToolErrorText(response).ShouldContain("JournalEntryWriteRefused");

        // delete_entry also walks /api/kits for the kit-owner guard; the refusal lands before that second read.
        Stub.Requests.ShouldNotContain(r => IsEntryMutation(r));
        Stub.Requests.ShouldNotContain(r => r.Path == "/api/kits");
    }

    [Fact]
    public async Task DeleteEntry_NoteGoesThroughTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("delete_entry", new JsonObject
        {
            ["entryId"] = Canned.EntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        FindRequest("DELETE", $"/api/agent/entries/{Canned.EntryId}");
    }

    [Fact]
    public async Task AckEntry_JournalEntry_StillWorksFromAnAgentSession()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/entries/{JournalEntryId}/ack",
            200,
            $$"""{"entryId":"{{JournalEntryId}}","ack":true,"alias":"scribe","vendor":"qwen","at":"2026-11-17T10:00:00Z"}""");

        var response = await Driver.CallToolAsync("ack_entry", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var result = response["result"]!;
        result["isError"].ShouldBeNull($"ack on a journal entry must not be gated: {result.ToJsonString()}");
        Stub.Requests.ShouldNotContain(r => r.Method == "DELETE");
    }

    [Fact]
    public async Task ToolSurface_JournalIsReadableButNeverWritable()
    {
        var tools = (await Driver.ListToolsAsync())["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var listProperties = tools["list_entries"]!["inputSchema"]!["properties"]!.AsObject();
        listProperties["type"]!["description"]!.GetValue<string>().ShouldContain("journal");

        foreach (var tool in new[] { "write_entry", "update_entry", "append_entry", "lift_entry", "delete_entry" })
        {
            var description = tools[tool]!["description"]!.GetValue<string>();
            description.Contains("agent door").ShouldBeTrue($"{tool} must name the door it calls");
            description.Contains("JournalEntryWriteRefused")
                .ShouldBeTrue($"{tool} must name the refusal it will hit");
        }

        var writeTypeDescription = tools["write_entry"]!["inputSchema"]!["properties"]!
            .AsObject()["type"]!["description"]!.GetValue<string>();
        writeTypeDescription.ShouldNotContain("journal");
    }

    // A request that would change an entry: the verbs the gate guards, on an entry path, minus the ack, which
    // writes an additive readBy row and must stay open.
    private static bool IsEntryMutation(StubOvermindApi.Request request) =>
        !request.Path.EndsWith("/ack") &&
        (request.Method == "POST" || request.Method == "PUT" || request.Method == "DELETE") &&
        (request.Path.Contains("/entries") || request.Path.Contains("/entries/"));

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
