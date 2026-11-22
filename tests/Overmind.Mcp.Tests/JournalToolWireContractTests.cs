using System.Text.Json;
using System.Text.Json.Nodes;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class JournalToolWireContractTests : McpSessionTestBase
{
    private static readonly Guid JournalEntryId = Guid.Parse("5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e");
    private static readonly Guid MissingEntryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid JournalContainerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnknownProjectId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private const string JournalProjectsJson =
        """
        [
          {"id":"11111111-1111-1111-1111-111111111111","name":"alpha","diskPath":"E:\\work\\alpha","description":"","manifest":null,"createdAt":"2026-09-01T10:00:00Z"},
          {"id":"77777777-7777-7777-7777-777777777777","name":"Journal","diskPath":"E:\\work\\journal","description":"the owner's reserved container","manifest":null,"createdAt":"2026-09-05T10:00:00Z"}
        ]
        """;

    private const string JournalRowJson =
        """
        {"id":"5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e","projectId":"77777777-7777-7777-7777-777777777777","type":"journal","title":"Daily Note","content":"moran thoughts","currentVersion":1,"createdAt":"2026-11-17T10:00:00Z","updatedAt":"2026-11-17T12:00:00Z","tags":["daily"],"skillFiles":[]}
        """;

    private const string JournalDetailJson =
        """
        {"id":"5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e","projectId":"77777777-7777-7777-7777-777777777777","type":"journal","title":"Daily Note","content":"moran thoughts","currentVersion":1,"createdAt":"2026-11-17T10:00:00Z","updatedAt":"2026-11-17T12:00:00Z","tags":["daily"],"skillFiles":[],"readBy":[],"pointers":[],"state":"active","enforces":false,"outgoing":[{"kind":"entry","id":"33333333-3333-3333-3333-333333333333","label":"Entry One"}],"incoming":[{"entryId":"1d1d1d1d-1d1d-1d1d-1d1d-1d1d1d1d1d1d","title":"Storage choice","snippet":"links to the daily note"}],"unresolved":["[[Ghost Note]]"]}
        """;

    private const string JournalListingJson =
        """
        [
          {"id":"5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e","type":"journal","title":"Daily Note","content":"moran thoughts","tags":["daily"],"createdAt":"2026-11-17T10:00:00Z","updatedAt":"2026-11-17T12:00:00Z"},
          {"id":"6e6e6e6e-6e6e-6e6e-6e6e-6e6e6e6e6e6e","type":"journal","title":"Evening Note","content":"more thoughts","tags":[],"createdAt":"2026-11-17T18:00:00Z","updatedAt":"2026-11-17T18:00:00Z"}
        ]
        """;

    protected override IReadOnlyDictionary<string, string>? EnvironmentOverrides =>
        new Dictionary<string, string> { [McpProfileResolver.EnvironmentVariable] = "journal" };

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        stub.RouteGet("/api/projects", JournalProjectsJson);
        stub.RouteGet($"/api/projects/{JournalContainerId}/entries", JournalListingJson);
        stub.RouteGet($"/api/entries/{JournalEntryId}", JournalDetailJson);
        stub.RouteGet($"/api/entries/{MissingEntryId}", Canned.NotFoundJson, 404);
        stub.Route("POST", "/api/entries", CaptureDoor);
        stub.Route("POST", $"/api/entries/{JournalEntryId}/append", AppendDoor);
        stub.Route("POST", $"/api/entries/{MissingEntryId}/append", 404, Canned.NotFoundJson);
    }

    [Fact]
    public async Task Capture_OmittedProjectId_FilesIntoTheReservedContainer()
    {
        var response = await Driver.CallToolAsync("journal_capture", new JsonObject
        {
            ["title"] = "Morning thought",
            ["content"] = "first idea of the day"
        });

        response["result"]!["isError"].ShouldBeNull($"capture failed: {response.ToJsonString()}");

        var payload = ResultPayload(response);
        payload["projectId"]!.GetValue<string>().ShouldBe(JournalContainerId.ToString());
        payload["title"]!.GetValue<string>().ShouldBe("Morning thought");

        var sent = JsonNode.Parse(FindRequest("POST", "/api/entries").Body)!;
        sent["type"]!.GetValue<string>().ShouldBe("journal");
        sent["projectId"].ShouldBeNull("the omitted projectId must stay omitted — the Host door births the container");
    }

    [Fact]
    public async Task Capture_OversizeContent_LandsAcceptedWithWarnings()
    {
        var response = await Driver.CallToolAsync("journal_capture", new JsonObject
        {
            ["title"] = "Big thought",
            ["content"] = new string('x', 2500)
        });

        response["result"]!["isError"].ShouldBeNull("an oversize capture is accepted, not refused");

        var payload = ResultPayload(response);
        payload["bodyChars"]!.GetValue<int>().ShouldBe(SoftCap.Keep(SoftCapLimits.FollowUpText));
        AssertCarriedWarning(payload, expectTruncated: true);
    }

    [Fact]
    public async Task Capture_UnknownProject_RefusesWithTheDoorMessage()
    {
        var response = await Driver.CallToolAsync("journal_capture", new JsonObject
        {
            ["title"] = "Misfiled thought",
            ["content"] = "body",
            ["projectId"] = UnknownProjectId.ToString()
        });

        ToolErrorText(response).ShouldContain($"Project '{UnknownProjectId}' was not found.");
    }

    [Fact]
    public async Task Search_Fulltext_ReturnsHitsAndNamesItsMode()
    {
        var response = await Driver.CallToolAsync("journal_search", new JsonObject
        {
            ["query"] = "hello"
        });

        response["result"]!["isError"].ShouldBeNull($"search failed: {response.ToJsonString()}");
        Canned.ToolResultText(response).ShouldContain("Entry One");

        var request = FindRequest("GET", "/api/search");
        var query = ParseQuery(request.Query);
        query["q"].ShouldBe("hello");
        query["mode"].ShouldBe("fulltext");
        query.ShouldNotContainKey("limit");
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsAnEmptyArray()
    {
        Stub.RouteGet("/api/search", """{"query":"ghost","hits":[]}""");

        var response = await Driver.CallToolAsync("journal_search", new JsonObject
        {
            ["query"] = "ghost"
        });

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldBe("[]");
    }

    [Fact]
    public async Task Read_ReturnsContentAndBothRails()
    {
        var response = await Driver.CallToolAsync("journal_read", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull($"read failed: {response.ToJsonString()}");

        var payload = ResultPayload(response);
        payload["content"]!.GetValue<string>().ShouldBe("moran thoughts");
        payload["outgoing"]!.AsArray().Count.ShouldBe(1);
        payload["outgoing"]![0]!["label"]!.GetValue<string>().ShouldBe("Entry One");
        payload["incoming"]![0]!["title"]!.GetValue<string>().ShouldBe("Storage choice");
        payload["unresolved"]![0]!.GetValue<string>().ShouldBe("[[Ghost Note]]");
    }

    [Fact]
    public async Task Read_MissingEntry_RefusesWithTheDoorMessage()
    {
        var response = await Driver.CallToolAsync("journal_read", new JsonObject
        {
            ["entryId"] = MissingEntryId.ToString()
        });

        ToolErrorText(response).ShouldContain($"Entry '{MissingEntryId}' was not found.");
    }

    [Fact]
    public async Task List_OmittedProjectId_ResolvesTheJournalContainer()
    {
        var response = await Driver.CallToolAsync("journal_list", new JsonObject());

        response["result"]!["isError"].ShouldBeNull($"list failed: {response.ToJsonString()}");
        Canned.ToolResultText(response).ShouldContain("Daily Note");

        Stub.Requests.ShouldContain(r => r.Method == "GET" && r.Path == "/api/projects");
        var request = FindRequest("GET", $"/api/projects/{JournalContainerId}/entries");
        request.Query.ShouldBeEmpty("no filters passed when none requested");
    }

    [Fact]
    public async Task List_WithoutAJournalContainer_RefusesWithNamedError()
    {
        Stub.RouteGet("/api/projects", Canned.ProjectsJson);

        var response = await Driver.CallToolAsync("journal_list", new JsonObject());

        ToolErrorText(response).ShouldContain("JournalProjectMissing");
    }

    [Fact]
    public async Task List_DayFilter_TranslatesToSinceUntilBounds()
    {
        await Driver.CallToolAsync("journal_list", new JsonObject { ["day"] = "2026-11-22" });

        var query = ParseQuery(FindRequest("GET", $"/api/projects/{JournalContainerId}/entries").Query);
        query["since"].ShouldBe("2026-11-22T00:00:00Z");
        query["until"].ShouldBe("2026-11-23T00:00:00Z");
    }

    [Fact]
    public async Task List_LimitCapsTheReturnedRows()
    {
        var response = await Driver.CallToolAsync("journal_list", new JsonObject { ["limit"] = 1 });

        response["result"]!["isError"].ShouldBeNull();
        var payload = ResultPayload(response);
        payload!.AsArray().Count.ShouldBe(1);
        payload[0]!["title"]!.GetValue<string>().ShouldBe("Daily Note");
    }

    [Fact]
    public async Task Append_OversizeText_LandsAcceptedWithWarnings()
    {
        var response = await Driver.CallToolAsync("journal_append", new JsonObject
        {
            ["entryId"] = JournalEntryId.ToString(),
            ["text"] = new string('y', 2500)
        });

        response["result"]!["isError"].ShouldBeNull("an oversize append is accepted, not refused");

        var payload = ResultPayload(response);
        payload["version"]!.GetValue<int>().ShouldBe(2);
        AssertCarriedWarning(payload, expectTruncated: true);
    }

    [Fact]
    public async Task Append_MissingEntry_RefusesWithTheDoorMessage()
    {
        var response = await Driver.CallToolAsync("journal_append", new JsonObject
        {
            ["entryId"] = MissingEntryId.ToString(),
            ["text"] = "follow-up for a ghost"
        });

        ToolErrorText(response).ShouldContain($"Entry '{MissingEntryId}' was not found.");

        FindRequest("POST", $"/api/entries/{MissingEntryId}/append");
        Stub.Requests.ShouldNotContain(
            r => r.Method == "POST" && r.Path.StartsWith("/api/agent/") && r.Path.Contains("/entries"),
            "the journal door speaks the human prefix only");
    }

    private static void AssertCarriedWarning(JsonNode payload, bool expectTruncated)
    {
        var warnings = payload["warnings"]!.AsArray();
        warnings.Count.ShouldBe(1);
        warnings[0]!["limit"]!.GetValue<int>().ShouldBe(SoftCapLimits.FollowUpText);
        warnings[0]!["accepted_len"]!.GetValue<int>().ShouldBe(SoftCap.Keep(SoftCapLimits.FollowUpText));
        warnings[0]!["truncated"]!.GetValue<bool>().ShouldBe(expectTruncated);
        warnings[0]!["lost_tail_preview"].ShouldNotBeNull();
    }

    private StubOvermindApi.Response CaptureDoor(StubOvermindApi.Request request)
    {
        var body = (JsonObject)JsonNode.Parse(request.Body)!;
        if (body["projectId"] is { } named && named.GetValue<string>() == UnknownProjectId.ToString())
        {
            return new StubOvermindApi.Response(404, $$"""{"title":"Not Found","detail":"Project '{{UnknownProjectId}}' was not found.","status":404}""");
        }

        if (body["type"]?.GetValue<string>() == "journal" && body["projectId"] is null)
        {
            body["projectId"] = JournalContainerId.ToString();
        }

        var outcome = SoftCap.Apply(body["content"]?.GetValue<string>(), SoftCapLimits.FollowUpText);
        body["content"] = outcome.Stored;
        body["id"] = JournalEntryId.ToString();
        body["currentVersion"] = 1;
        body["createdAt"] = "2026-11-22T10:00:00Z";
        body["updatedAt"] = "2026-11-22T10:00:00Z";
        if (outcome.Warning is { } warning)
        {
            body["warnings"] = new JsonArray(JsonSerializer.SerializeToNode(warning)!);
        }

        return new StubOvermindApi.Response(201, body.ToJsonString());
    }

    private StubOvermindApi.Response AppendDoor(StubOvermindApi.Request request)
    {
        var text = ((JsonObject)JsonNode.Parse(request.Body)!)["text"]?.GetValue<string>() ?? string.Empty;
        var outcome = SoftCap.Apply(text, SoftCapLimits.FollowUpText);
        var appended = JsonNode.Parse(JournalRowJson)!.AsObject();
        appended["id"] = JournalEntryId.ToString();
        appended["type"] = "journal";
        appended["title"] = "Daily Note";
        appended["currentVersion"] = 2;
        appended["updatedAt"] = "2026-11-22T10:00:00Z";
        appended["content"] = "moran thoughts\n\n## Follow-up 2026-11-22\n" + outcome.Stored;
        if (outcome.Warning is { } warning)
        {
            appended["warnings"] = new JsonArray(JsonSerializer.SerializeToNode(warning)!);
        }

        return new StubOvermindApi.Response(200, appended.ToJsonString());
    }

    private static JsonNode ResultPayload(JsonNode response) =>
        JsonNode.Parse(Canned.ToolResultText(response))
        ?? throw new InvalidOperationException($"tool returned no structured payload: {response.ToJsonString()}");

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
