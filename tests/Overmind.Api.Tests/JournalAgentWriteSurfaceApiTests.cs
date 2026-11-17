using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Spec R6 as an invariant, proven from the registered endpoint table instead of a verb list: the mutation cases
// below are enumerated from the RouteEndpoint collection, so an agent entry route that nobody gated is picked up
// and turns this class red without anyone editing a list first. The injection that demonstrates that is recorded
// at the bottom of the file.
public sealed class JournalAgentWriteSurfaceApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // A journal refusal has to arrive before a validator or a handler can complain about shape, so one payload
    // that satisfies every id-addressed mutation contract is enough to prove the gate rather than its grammar.
    private const string IdAddressedBody =
        """{"title":"Rewritten by the agent","content":"agent payload","text":"agent follow-up","reason":"agent reason"}""";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    public JournalAgentWriteSurfaceApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
        _projectId = CreateProject("surface-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public void AgentEntryRoutes_AreDerivedFromTheRegisteredEndpointTable()
    {
        var cases = MutationCases();

        // The denominator: a prefix rename would silently empty the derived set and every test below would then
        // pass on nothing. The verbs the gate must cover are named here, not in the gate.
        cases.ShouldNotBeEmpty();
        cases.Select(c => c.Method).ShouldContain("DELETE");
        cases.ShouldContain(c => c.Pattern == "/api/agent/entries" && c.Method == "POST");
        cases.ShouldContain(c => c.Pattern == "/api/agent/entries/{id:guid}" && c.Method == "PUT");
        cases.ShouldContain(c => c.Pattern == "/api/agent/entries/{id:guid}" && c.Method == "DELETE");
        cases.ShouldContain(c => c.Pattern == "/api/agent/entries/{id:guid}/lift" && c.Method == "POST");
        cases.ShouldContain(c => c.Pattern == "/api/agent/entries/{id:guid}/append" && c.Method == "POST");
    }

    [Fact]
    public async Task AgentEntryRoutes_EveryRegisteredMutation_RefusesJournalBeforeTouchingTheRow()
    {
        foreach (var (method, pattern, addressable) in MutationCases())
        {
            var journalId = await CreateJournalNoteAsync("Owner note", "owned");
            var body = addressable
                ? IdAddressedBody
                : $$"""{"projectId":"{{_projectId}}","type":"journal","title":"Agent-authored","content":"nope"}""";

            var response = await SendAsync(method, BuildPath(pattern, journalId), body);
            var detail = $"{method} {pattern} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, detail);
            (await ReadProblemAsync(response)).Title.ShouldBe("JournalEntryWriteRefused", detail);
            (await ContentOfAsync(journalId)).ShouldBe("owned", detail);
        }
    }

    [Fact]
    public async Task AgentEntryRoutes_NoneOfTheDerivedCasesIsASafeMethod_AcksStayOutsideTheGate()
    {
        var methods = MutationCases().Select(c => c.Method).Distinct().ToArray();

        foreach (var method in methods)
        {
            HttpMethods.IsGet(method).ShouldBeFalse();
            HttpMethods.IsHead(method).ShouldBeFalse();
        }

        // The ack route addresses an entry by id under /api/agent as well, and it must NOT join the mutation set:
        // an ack writes an additive readBy row, not the entry.
        AllEndpoints().ShouldContain(e =>
            e.RoutePattern.RawText!.StartsWith("/api/agent/projects/") && e.RoutePattern.RawText!.EndsWith("/ack"));
        MutationCases().ShouldNotContain(c => c.Pattern.EndsWith("/ack"));

        var journalId = await CreateJournalNoteAsync("Daily note", "moran thoughts");
        var sessionId = await RegisterSessionAsync("surface-marshal", "qwen");
        var acked = await PostWithSessionAsync(
            $"/api/agent/projects/{EnsureJournalProject()}/entries/{journalId}/ack", "{}", sessionId);

        acked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ContentOfAsync(journalId)).ShouldBe("moran thoughts");
    }

    [Theory]
    [InlineData("update")]
    [InlineData("append")]
    [InlineData("lift")]
    [InlineData("delete")]
    public async Task JournalTarget_AgentDoorIsRefused_HumanDoorStillMutatesIt(string verb)
    {
        var journalId = await CreateJournalNoteAsync("Owner note", "owned body");

        var refused = await SendAsync(WireMethod(verb), AgentPath(verb, journalId), BodyFor(verb, "agent tried"));
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(refused)).Title.ShouldBe("JournalEntryWriteRefused");
        (await ContentOfAsync(journalId)).ShouldBe("owned body");

        var human = await SendAsync(WireMethod(verb), HumanPath(verb, journalId), BodyFor(verb, "owner wrote it"));
        var body = await human.Content.ReadAsStringAsync();
        human.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task JournalTarget_CreateOnTheAgentDoorIsRefused_NamingTheAllowedTypes()
    {
        var response = await SendAsync("POST", "/api/agent/entries",
            $$"""{"projectId":"{{_projectId}}","type":"journal","title":"Agent journal","content":"nope"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.Title.ShouldBe("JournalEntryWriteRefused");
        problem.Detail.ShouldContain("note, decision, gate, guard, idea, audit, tombstone");
        problem.Detail.ShouldNotContain("journal,");

        var human = await SendAsync("POST", "/api/entries",
            $$"""{"projectId":"{{EnsureJournalProject()}}","type":"journal","title":"Owner journal","content":"fine"}""");
        human.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("append")]
    [InlineData("lift")]
    [InlineData("delete")]
    public async Task NoteTarget_BothDoorsAnswerTheSameWay(string verb)
    {
        var throughAgent = await CreateEntryAsync("note", "Agent side", "body");
        var throughHuman = await CreateEntryAsync("note", "Human side", "body");

        var agentResponse = await SendAsync(WireMethod(verb), AgentPath(verb, throughAgent), BodyFor(verb, "agent payload"));
        var agentBody = await agentResponse.Content.ReadAsStringAsync();
        agentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, agentBody);

        var humanResponse = await SendAsync(WireMethod(verb), HumanPath(verb, throughHuman), BodyFor(verb, "human payload"));
        var humanBody = await humanResponse.Content.ReadAsStringAsync();
        humanResponse.StatusCode.ShouldBe(HttpStatusCode.OK, humanBody);

        PropertyNames(agentBody).ShouldBe(PropertyNames(humanBody), verb);
    }

    [Fact]
    public async Task AgentDoor_AppendFollowUp_StillSnapshotsVersionAndRecomputesEdges()
    {
        var anchorId = await CreateJournalNoteAsync("Anchor", "the anchor");
        var noteId = await CreateEntryAsync("note", "Linking note", "links [[Anchor]]");
        (await DerivedRowsToAsync(anchorId, noteId)).ShouldBe(1, "created body already carries the marker");

        var appended = await _client.PostAsJsonAsync(
            $"/api/agent/entries/{noteId}/append", new { text = "one more thing [[Anchor]]" });
        var body = await appended.Content.ReadAsStringAsync();

        appended.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var entry = await appended.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry!.Content.ShouldContain("## Follow-up");
        entry.Content.ShouldContain("one more thing [[Anchor]]");
        entry.CurrentVersion.ShouldBe(1);

        var versions = await _client.GetFromJsonAsync<List<EntryVersionJson>>(
            $"/api/entries/{noteId}/versions", JsonOptions);
        versions!.Select(v => v.Version).ShouldBe([1]);

        (await DerivedRowsToAsync(anchorId, noteId)).ShouldBe(1, "the appended marker lands a derived edge");
    }

    [Fact]
    public async Task AgentDoor_DeleteNote_StillProducesATombstoneCarryingTheFinalContent()
    {
        var sessionId = await RegisterSessionAsync("surface-scribe", "qwen");
        var noteId = await CreateEntryAsync("note", "Doomed note", "the last body");

        var response = await SendWithSessionAsync(
            HttpMethod.Delete, $"/api/agent/entries/{noteId}?reason=retired", "{}", sessionId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var tombstone = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone!.Type.ShouldBe("tombstone");
        tombstone.Title.ShouldBe("Doomed note (deleted)");
        tombstone.Content.ShouldContain("the last body");
        tombstone.Content.ShouldContain("via agent door");
        tombstone.Content.ShouldContain("by surface-scribe (qwen)");
        tombstone.Content.ShouldContain("Reason: retired");
        tombstone.Content.ShouldContain("Original type: note");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.ContextEntries.AnyAsync(e => e.Id == noteId)).ShouldBeFalse();
    }

    [Fact]
    public async Task AgentDoor_AckJournalEntry_SucceedsAndJournalReadsStayOpen()
    {
        var journalProjectId = EnsureJournalProject();
        var journalId = await CreateJournalNoteAsync("Daily note", "moran thoughts");
        var sessionId = await RegisterSessionAsync("surface-reader", "qwen");

        var acked = await PostWithSessionAsync(
            $"/api/agent/projects/{journalProjectId}/entries/{journalId}/ack", "{}", sessionId);
        var body = await acked.Content.ReadAsStringAsync();

        acked.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        Element(body).GetProperty("ack").GetBoolean().ShouldBeTrue();
        (await ContentOfAsync(journalId)).ShouldBe("moran thoughts");

        var listed = await _client.GetAsync($"/api/projects/{journalProjectId}/entries?type=journal");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/entries/{journalId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/entries/{journalId}/versions")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/api/search?q=moran")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AgentDoor_UnknownEntryId_StillAnswersNotFound_NotAJournalRefusal()
    {
        var response = await _client.PostAsync(
            $"/api/agent/entries/{Guid.NewGuid()}/append",
            new StringContent("""{"text":"x"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response)).Title.ShouldNotBe("JournalEntryWriteRefused");
    }

    private sealed record MutationCase(string Method, string Pattern, bool Addressable);

    private List<MutationCase> MutationCases()
    {
        var cases = new List<MutationCase>();

        foreach (var endpoint in AllEndpoints())
        {
            var pattern = endpoint.RoutePattern.RawText!;
            if (!pattern.StartsWith("/api/agent/entries", StringComparison.Ordinal))
            {
                continue;
            }

            var verbMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            if (verbMetadata is null)
            {
                continue;
            }

            foreach (var method in verbMetadata.HttpMethods)
            {
                if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
                {
                    continue;
                }

                cases.Add(new MutationCase(method, pattern, HasEntryIdParameter(pattern)));
            }
        }

        return cases;
    }

    private IEnumerable<RouteEndpoint> AllEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    private static bool HasEntryIdParameter(string pattern) =>
        pattern.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.StartsWith('{') &&
                (segment is "{id}" or "{id:guid}" or "{entryId}" or "{entryId:guid}"));

    private static string BuildPath(string pattern, Guid entryId)
    {
        var segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment =>
            {
                if (!segment.StartsWith('{'))
                {
                    return segment;
                }

                var name = segment.TrimStart('{').Split(':', '}')[0];
                return name is "id" or "entryId" ? entryId.ToString() : Guid.NewGuid().ToString();
            })
            .ToList();

        segments.Insert(0, string.Empty);
        return string.Join('/', segments);
    }

    private static string AgentPath(string verb, Guid id) => verb switch
    {
        "update" => $"/api/agent/entries/{id}",
        "append" => $"/api/agent/entries/{id}/append",
        "lift" => $"/api/agent/entries/{id}/lift",
        "delete" => $"/api/agent/entries/{id}",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "unknown verb in the matrix"),
    };

    private static string HumanPath(string verb, Guid id) => verb switch
    {
        "update" => $"/api/entries/{id}",
        "append" => $"/api/entries/{id}/append",
        "lift" => $"/api/entries/{id}/lift",
        "delete" => $"/api/entries/{id}",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "unknown verb in the matrix"),
    };

    private static string BodyFor(string verb, string payload) => verb switch
    {
        "update" => $$"""{"title":"Retitled","content":"{{payload}}"}""",
        "append" => $$"""{"text":"{{payload}}"}""",
        "lift" => $$"""{"reason":"{{payload}}"}""",
        "delete" => "{}",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "unknown verb in the matrix"),
    };

    private async Task<HttpResponseMessage> SendAsync(string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request);
    }

    private static string WireMethod(string verb) => verb switch
    {
        "create" or "append" or "lift" => "POST",
        "update" => "PUT",
        "delete" => "DELETE",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "unknown verb in the matrix"),
    };

    private Task<HttpResponseMessage> SendWithSessionAsync(
        HttpMethod method, string path, string body, Guid sessionId)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostWithSessionAsync(string path, string body, Guid sessionId) =>
        SendWithSessionAsync(HttpMethod.Post, path, body, sessionId);

    private async Task<int> DerivedRowsToAsync(Guid targetId, Guid sourceId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .EntryLinks
            .CountAsync(l => l.FromEntryId == sourceId && l.ToId == targetId);
    }

    private async Task<string> ContentOfAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.Content)
            .SingleAsync();
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult()!.Id;
    }

    private Guid EnsureJournalProject()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<JournalProjectStore>()
            .EnsureIdAsync()
            .GetAwaiter()
            .GetResult();
    }

    private async Task<Guid> CreateJournalNoteAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = EnsureJournalProject(),
            Type = "journal",
            Title = title,
            Content = content,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        return entry!.Id;
    }

    private async Task<Guid> CreateEntryAsync(string type, string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = type,
            Title = title,
            Content = content,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        return entry!.Id;
    }

    private async Task<Guid> RegisterSessionAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = vendor });

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionJson>(JsonOptions);
        return session!.Id;
    }

    private static List<string> PropertyNames(string body) =>
        [.. Element(body).EnumerateObject().Select(property => property.Name)];

    private static JsonElement Element(string body) => JsonDocument.Parse(body).RootElement;

    private static async Task<ProblemJson> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        return problem!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record SessionJson(Guid Id);

    public sealed record ProblemJson(string? Title, string? Detail, int Status);

    public sealed record EntryVersionJson(int Version, string Content, DateTime CreatedAt);

    public sealed record EntryJson(Guid Id, string Type, string Title, string Content, int CurrentVersion);
}

// INJECTION PROOF (contract step 6). Add one line to EntriesApi.MapEntriesApi, after the agentWrites block:
//
//     agent.MapPost("/entries/{id:guid}/star", (Guid id) => Results.Ok(new { starred = id }));
//
// The route is outside the gated subgroup, so it answers 200 for a journal target. The derived enumeration picks
// it up anyway and AgentEntryRoutes_EveryRegisteredMutation_RefusesJournalBeforeTouchingTheRow goes red naming the
// /star case. Removing the line returns the class to green; both runs are recorded in the task log.
