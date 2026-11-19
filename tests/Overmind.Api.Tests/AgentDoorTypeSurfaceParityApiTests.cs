using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.Api;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Three sources used to describe the same boundary and none of them was checked against the others: the writable
// set declared in Core, the refusal list inside the gate, and what the routes actually answer. The gate also
// classified only the entry its route names, so a verb carrying a second target in its body was waved through with
// a green route table. This file pins all of it shut: every EntryType is asked of the real door and the answer has
// to agree with both declarations, which turns an unclassified new member into a red test instead of a silent hole.
public sealed class AgentDoorTypeSurfaceParityApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly (string Verb, HttpMethod Method, string Suffix, string Body)[] IdAddressedVerbs =
    [
        ("update", HttpMethod.Put, "", """{"title":"Rewritten","content":"agent payload"}"""),
        ("append", HttpMethod.Post, "/append", """{"text":"agent follow-up"}"""),
        ("lift", HttpMethod.Post, "/lift", """{"reason":"agent retired it"}"""),
        ("delete", HttpMethod.Delete, "", "{}"),
    ];

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    public AgentDoorTypeSurfaceParityApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
        _projectId = CreateProject("parity-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public static IEnumerable<object[]> EveryEntryType() =>
        Enum.GetValues<EntryType>().Select(type => new object[] { type });

    [Fact]
    public static void TheGateRefusalListAndTheWritableSetPartitionTheEnum_WithoutGapOrOverlap()
    {
        var writable = EntryTypeSurface.AgentWritable;
        var refused = JournalWriteLockFilter.Gated.Keys.ToArray();

        writable.Intersect(refused).ToArray().ShouldBeEmpty();
        Names(writable.Concat(refused)).ShouldBe(Names(Enum.GetValues<EntryType>()),
            "every EntryType must be classified by one of the two declarations — an unclassified member is the " +
            "silent hole this pin exists to close");

        // What the door tells the caller it accepts has to stay the same sentence as what Core declares.
        foreach (var gated in JournalWriteLockFilter.Gated.Values)
        {
            gated.Message.ShouldContain(EntryTypeSurface.AgentWritableNames);
        }
    }

    private static string Names(IEnumerable<EntryType> types) =>
        string.Join(", ", types.Select(EntryTypeSurface.Normalize).Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(EveryEntryType))]
    public async Task AgentDoorCreate_EveryDeclaredType_AnswersTheWayBothListsSayItMust(EntryType type)
    {
        // One request per member with the expectation computed from the declarations, never hand-written: the
        // shouldContain below is what makes a new member that entered neither list fail by name instead of
        // passing by silence.
        var response = await _client.PostAsJsonAsync("/api/agent/entries", CreateBody(type, _projectId), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        if (JournalWriteLockFilter.Gated.TryGetValue(type, out var gated))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{type}: {body}");
            ReadTitle(body).ShouldBe(gated.Code, $"the door refused {type}, but not with its declared code");
            (await RowExistsAsync(ParityTitle(type))).ShouldBeFalse("a refusal writes nothing");
            return;
        }

        EntryTypeSurface.AgentWritable.Contains(type).ShouldBeTrue(
            $"{type} is neither refused by the gate nor declared writable — the door answered {body}");
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"{type}: {body}");
    }

    [Theory]
    [MemberData(nameof(EveryEntryType))]
    public async Task HumanDoor_EveryDeclaredType_StillLands_TheGateIsOnlyOnTheAgentPrefix(EntryType type)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/entries", CreateBody(type, ReservedContainerId()), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"{type}: {body}");
    }

    [Theory]
    [InlineData("update")]
    [InlineData("append")]
    [InlineData("lift")]
    [InlineData("delete")]
    public async Task AgentDoorIdAddressedVerbs_EveryDeclaredType_AnswersTheWayBothListsSayItMust(string verb)
    {
        // The skill half of I-E on the verbs that address a row instead of naming a type: both adapters have
        // always told the caller the UI owns skills, and until this gate read the target's type the server quietly
        // agreed to write them anyway — including overwriting their files through update.
        //
        // The walk covers the enum and derives its expectation from the two declarations, not from the gate's own
        // list: a pin that loops over Gated simply loses the skill case when skill leaves that list, and losing it
        // silently is exactly the drift this has to catch.
        var call = IdAddressedVerbs.Single(candidate => candidate.Verb == verb);

        foreach (var type in Enum.GetValues<EntryType>())
        {
            var entryId = await CreateThroughHumanDoorAsync(type, $"{verb} target {type}");

            var response = await SendAsync(call.Method, $"/api/agent/entries/{entryId}{call.Suffix}", call.Body);
            var body = await response.Content.ReadAsStringAsync();

            if (JournalWriteLockFilter.Gated.TryGetValue(type, out var gated))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{verb} on a {type} row: {body}");
                ReadTitle(body).ShouldBe(gated.Code, $"{verb} refused {type} with the wrong code");
                (await ContentOfAsync(entryId)).ShouldBe("owned body",
                    $"{verb} must not have touched the {type} row");
                continue;
            }

            EntryTypeSurface.AgentWritable.Contains(type).ShouldBeTrue(
                $"{type} went through this door as if it were writable, yet Core never declared it so — {body}");

            // Not "must succeed": a handler owns its own refusals (a tombstone has no lifecycle to lift), and one
            // of them appearing here is not this gate's business. What is not allowed is a gate code answering
            // for a type the gate declares no rule about.
            JournalWriteLockFilter.Gated.Values
                .Select(entry => entry.Code)
                .ShouldNotContain(ReadTitle(body),
                    $"{verb} on a {type} row was stopped by the gate, which has no rule for {type}: {body}");
        }
    }

    [Fact]
    public async Task AgentDoorLift_NamingAJournalRowAsTheSupersedeTarget_WritesOnlyTheAddressedRow()
    {
        // The one vetted body that carries a second entry id, and the reason it stays vetted: lifting marks the
        // addressed row superseded and only reads the target. That is the claim VettedBodies makes about
        // EntryLiftRequest — if supersede ever gains a reverse write, this goes red and the gate has to learn the
        // second target instead of keeping the vetting.
        //
        // Both rows sit in one project because the handler refuses a cross-project supersede outright
        // ("belongs to a different project"), which is a narrower multi-target surface than the vetting assumes —
        // recorded in the task's obstacles.
        var containerId = ReservedContainerId();
        var journalId = await CreateThroughHumanDoorAsync(containerId, EntryType.Journal, "Owner note");
        var noteId = await CreateThroughHumanDoorAsync(containerId, EntryType.Note, "Agent-owned note");
        var journalBefore = await RowSnapshotAsync(journalId);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/agent/entries/{noteId}/lift",
            $$"""{"reason":"retired in favour of the owner's note","supersededById":"{{journalId}}"}""");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var lifted = await RowSnapshotAsync(noteId);
        lifted.State.ShouldBe(ContextEntry.SupersededState);
        lifted.SupersededById.ShouldBe(journalId);

        (await RowSnapshotAsync(journalId)).ShouldBe(journalBefore,
            "the supersede target is read, never written — R6 holds on the secondary reference too");
    }

    [Fact]
    public void RegisteredAgentEntryVerbs_EveryDeclaredBodyIsOneTheGateHasVetted()
    {
        // The route table is the denominator, so a verb registered on the gated subgroup with a body nobody read
        // is named here at test time and not only refused at run time.
        var unvetted = new List<string>();

        foreach (var endpoint in Endpoints().Where(e =>
                     e.RoutePattern.RawText!.StartsWith("/api/agent/entries", StringComparison.Ordinal)))
        {
            if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } verbs ||
                verbs.HttpMethods.All(HttpMethods.IsGet))
            {
                continue;
            }

            var declared = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;
            if (declared is not null && !JournalWriteLockFilter.VettedBodies.Contains(declared))
            {
                unvetted.Add($"{string.Join("/", verbs.HttpMethods)} {endpoint.RoutePattern.RawText} → {declared.Name}");
            }
        }

        // The denominator in the other direction: the vetting would be vacuous if no route declared a body at all.
        var declaredBodies = Endpoints()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/agent/entries", StringComparison.Ordinal))
            .Select(e => e.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType)
            .Where(declared => declared is not null)
            .ToArray();

        declaredBodies.Length.ShouldBe(4,
            "create, update, lift and append declare bodies; anything else means the gate's vetted list and the " +
            "registered verbs drifted apart");
        unvetted.ShouldBeEmpty(string.Join(" | ", unvetted));
    }

    private static object CreateBody(EntryType type, Guid projectId) => new
    {
        ProjectId = projectId,
        Type = EntryTypeSurface.Normalize(type),
        Title = ParityTitle(type),
        Content = "probe body",
        SkillFiles = type == EntryType.Skill
            ? new[] { new { Platform = (string?)null, RelativePath = "SKILL.md", Content = "body" } }
            : null,
    };

    private static string ParityTitle(EntryType type) => $"Parity {type}";

    // A 204 from the delete verb carries no body at all, and "no problem reported" is not a gate code.
    private static string? ReadTitle(string body) =>
        body.Length == 0
            ? null
            : JsonDocument.Parse(body).RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;

    private Task<Guid> CreateThroughHumanDoorAsync(EntryType type, string title) =>
        CreateThroughHumanDoorAsync(
            type == EntryType.Journal ? ReservedContainerId() : _projectId, type, title);

    private async Task<Guid> CreateThroughHumanDoorAsync(Guid projectId, EntryType type, string title)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/entries",
            new
            {
                ProjectId = projectId,
                Type = EntryTypeSurface.Normalize(type),
                Title = title,
                Content = "owned body",
                SkillFiles = type == EntryType.Skill
                    ? new[] { new { Platform = (string?)null, RelativePath = "SKILL.md", Content = "body" } }
                    : null,
            },
            JsonOptions);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
    }

    private Guid ReservedContainerId()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<JournalProjectStore>()
            .EnsureIdAsync()
            .GetAwaiter()
            .GetResult();
    }

    private async Task<bool> RowExistsAsync(string title)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AnyAsync(e => e.Title == title);
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

    private async Task<RowSnapshot> RowSnapshotAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AsNoTracking()
            .SingleAsync(e => e.Id == id);

        return new RowSnapshot(
            row.Title,
            row.Content,
            row.State,
            row.SupersededById,
            row.UpdatedAt,
            row.CurrentVersion);
    }

    private IEnumerable<RouteEndpoint> Endpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string body)
    {
        using var request = new HttpRequestMessage(method, path);

        // DELETE takes no body — the route reads its reason from the query string, and a DELETE payload is bound
        // by nobody (the defect EmbeddedEntryToolDeleteApiTests closes).
        if (method != HttpMethod.Delete)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request);
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

    public sealed record ProjectJson(Guid Id);

    public sealed record RowSnapshot(
        string Title,
        string Content,
        string State,
        Guid? SupersededById,
        DateTime UpdatedAt,
        int CurrentVersion);
}
