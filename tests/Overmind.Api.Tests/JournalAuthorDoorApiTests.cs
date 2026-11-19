using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class JournalAuthorDoorApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _journalId;

    public JournalAuthorDoorApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
        _projectId = CreateProject("door-project");
        _journalId = EnsureJournalProject();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task AgentDoor_PostJournal_IsRefusedWithTypedProblemNamingTheAllowedTypes()
    {
        var response = await PostAgentAsync("journal", "Agent-authored note", "should never land");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.Status.ShouldBe(400);
        problem.Title.ShouldBe("JournalEntryWriteRefused");
        problem.Detail.ShouldContain("note, decision, gate, guard, idea, audit, tombstone");
        problem.Detail.ShouldNotContain("journal,");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.ContextEntries.AnyAsync(e => e.Title == "Agent-authored note")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("note")]
    [InlineData("decision")]
    [InlineData("gate")]
    [InlineData("guard")]
    [InlineData("idea")]
    [InlineData("audit")]
    [InlineData("tombstone")]
    public async Task AgentDoor_AuthorAllowedTypes_Succeed(string type)
    {
        var response = await PostAgentAsync(type, $"{type} via the agent door", "body text");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task AgentDoor_PostWithSessionHeader_StillAttributesTheAlias()
    {
        var sessionId = await RegisterSessionAsync("door-scribe", "qwen");

        var response = await PostAgentAsync("note", "Attributed via door", "body", sessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry!.AuthorAlias.ShouldBe("door-scribe");
    }

    [Fact]
    public async Task HumanDoor_CreatesJournalEntry_AndStampsNoAuthorAlias()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _journalId,
            Type = "journal",
            Title = "Owner note",
            Content = "owned surface",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        entry!.Type.ShouldBe("journal");
        entry.AuthorAlias.ShouldBeNull();
    }

    [Fact]
    public async Task AgentDoor_UpdateRefusesJournalEntry_HumanDoorStillUpdatesIt()
    {
        var noteId = await CreateJournalNoteAsync("Owner note", "first");

        var refused = await _client.PutAsJsonAsync($"/api/agent/entries/{noteId}", new
        {
            Title = "Owner note",
            Content = "agent rewrote the journal",
        });
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(refused)).Title.ShouldBe("JournalEntryWriteRefused");

        var allowed = await _client.PutAsJsonAsync($"/api/entries/{noteId}", new
        {
            Title = "Owner note",
            Content = "owner rewrote it",
        });
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var content = await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .ContextEntries.AsNoTracking()
            .Where(e => e.Id == noteId)
            .Select(e => e.Content)
            .SingleAsync();
        content.ShouldBe("owner rewrote it");
    }

    [Fact]
    public async Task BothDoors_NonJournalWrite_ProduceOneShapeOneVersionCurveAndBothIndexed()
    {
        const string marker = "quixotery";

        var human = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = "Door parity human",
            Content = $"body mentioning {marker} human",
        });
        var agent = await PostAgentAsync("note", "Door parity agent", $"body mentioning {marker} agent");

        human.StatusCode.ShouldBe(HttpStatusCode.Created);
        agent.StatusCode.ShouldBe(HttpStatusCode.Created);

        var humanEntry = await human.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        var agentEntry = await agent.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        humanEntry!.CurrentVersion.ShouldBe(agentEntry!.CurrentVersion);
        humanEntry.State.ShouldBe(agentEntry.State);
        humanEntry.Outgoing.ShouldBeEmpty();
        agentEntry.Outgoing.ShouldBeEmpty();

        var humanUpdate = await _client.PutAsJsonAsync($"/api/entries/{humanEntry.Id}", new
        {
            Title = "Door parity human",
            Content = $"body mentioning {marker} human rewritten",
        });
        var agentUpdate = await _client.PutAsJsonAsync($"/api/agent/entries/{agentEntry.Id}", new
        {
            Title = "Door parity agent",
            Content = $"body mentioning {marker} agent rewritten",
        });

        humanUpdate.StatusCode.ShouldBe(HttpStatusCode.OK);
        agentUpdate.StatusCode.ShouldBe(HttpStatusCode.OK);
        var humanBumpedBody = await humanUpdate.Content.ReadAsStringAsync();
        var agentBumpedBody = await agentUpdate.Content.ReadAsStringAsync();
        var humanBumped = JsonSerializer.Deserialize<EntryJson>(humanBumpedBody, JsonOptions);
        var agentBumped = JsonSerializer.Deserialize<EntryJson>(agentBumpedBody, JsonOptions);
        humanBumped!.CurrentVersion.ShouldBe(agentBumped!.CurrentVersion);
        humanBumped.CurrentVersion.ShouldBe(1);
        humanBumped.Content.ShouldBe("body mentioning quixotery human rewritten");
        agentBumped.Content.ShouldBe("body mentioning quixotery agent rewritten");

        var shapeOfHuman = await ResponsePropertyNames($"/api/entries/{humanEntry.Id}");
        var shapeOfAgent = await ResponsePropertyNames($"/api/entries/{agentEntry.Id}");
        shapeOfHuman.ShouldBe(shapeOfAgent);

        var versions = await _client.GetFromJsonAsync<List<EntryVersionJson>>(
            $"/api/entries/{agentEntry.Id}/versions", JsonOptions);
        versions!.Select(version => version.Version).ShouldBe([1]);

        var hits = await _client.GetFromJsonAsync<SearchJson>($"/api/search?q={marker}", JsonOptions);
        hits!.Hits.Select(hit => hit.EntryId).ShouldContain(humanEntry.Id);
        hits.Hits.Select(hit => hit.EntryId).ShouldContain(agentEntry.Id);

        var refused = await _client.PutAsJsonAsync($"/api/entries/{humanEntry.Id}", new { Content = "" });
        var refusedThroughAgent = await _client.PutAsJsonAsync($"/api/agent/entries/{agentEntry.Id}", new { Content = "" });
        refused.StatusCode.ShouldBe(refusedThroughAgent.StatusCode);
        (await ReadProblemAsync(refused)).Title.ShouldBe((await ReadProblemAsync(refusedThroughAgent)).Title);
    }

    [Fact]
    public async Task DeleteReservedJournalProject_IsProtected_OtherProjectsStillDelete()
    {
        var protectedDelete = await _client.DeleteAsync($"/api/projects/{_journalId}");

        protectedDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadProblemAsync(protectedDelete)).Title.ShouldBe("ReservedProjectProtected");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            (await db.Projects.AnyAsync(p => p.Id == _journalId)).ShouldBeTrue();
        }

        var scratch = CreateProject("disposable-project");
        (await _client.DeleteAsync($"/api/projects/{scratch}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteJournalNote_ConvertsToTombstone_AndItsOutgoingEdgesVanishByConstraint()
    {
        var target = await CreateJournalNoteAsync("Anchor Note", "the target");
        await CreateFeatureAsync("888-door-pin", "Door pin");
        var source = await CreateJournalNoteAsync("Linking Note", "links [[Anchor Note]] and [[feature:888]]");

        await CreateJournalNoteAsync("Readers Note", "reads [[Linking Note]]");
        (await DerivedEdgeCountAsync(source)).ShouldBe(2);

        var deleted = await _client.DeleteAsync($"/api/entries/{source}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tombstone = await deleted.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone!.Type.ShouldBe("tombstone");

        (await DerivedEdgeCountAsync(source)).ShouldBe(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var danglingRows = await db.EntryLinks
            .CountAsync(link => link.ToKind == EntryLinkKind.Entry && link.ToId == source);
        (await db.ContextEntries.AnyAsync(e => e.Title == "Readers Note")).ShouldBeTrue();
        danglingRows.ShouldBe(1);

        var targetDetail = await GetDetailAsync(target);
        targetDetail.Incoming.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnresolvedPrefixedMarkers_RenderWithTheNormalizedPrefixCasing()
    {
        var noteId = await CreateJournalNoteAsync(
            "Casing Note", "see [[Feature:99991]] and [[PROJECT:nowhere-project]]");

        var detail = await GetDetailAsync(noteId);

        detail.Unresolved.ShouldNotContain("Feature:99991");
        detail.Unresolved.ShouldNotContain("PROJECT:nowhere-project");
        detail.Unresolved.ShouldContain("feature:99991");
        detail.Unresolved.ShouldContain("project:nowhere-project");
    }

    [Fact]
    public async Task AgentDoor_CreateWithMarkers_PersistsEdgesThroughTheSameCommit()
    {
        var target = await CreateJournalNoteAsync("Anchor Note", "the target");
        var featureId = await CreateFeatureAsync("888-door-pin", "Door pin");

        var response = await PostAgentAsync("note", "Agent note with edges", "links [[Anchor Note]] and [[feature:888]]");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        var rows = await DerivedRowsAsync(entry!.Id);
        rows.Count.ShouldBe(2);
        rows.ShouldContain(row => row.Kind == "feature" && row.Id == featureId);
        rows.ShouldContain(row => row.Kind == "entry" && row.Id == target);
    }

    private Task<HttpResponseMessage> PostAgentAsync(string type, string title, string content, Guid? sessionId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/entries")
        {
            Content = JsonContent.Create(new
            {
                ProjectId = _projectId,
                Type = type,
                Title = title,
                Content = content,
            })
        };

        if (sessionId.HasValue)
        {
            request.Headers.Add("X-Overmind-Session", sessionId.Value.ToString());
        }

        return _client.SendAsync(request);
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

    private async Task<Guid> RegisterSessionAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = vendor });

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionJson>(JsonOptions);
        return session!.Id;
    }

    private async Task<Guid> CreateJournalNoteAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _journalId,
            Type = "journal",
            Title = title,
            Content = content,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        return entry!.Id;
    }

    private async Task<Guid> CreateFeatureAsync(string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .PlanningFeatures.AsNoTracking()
            .Where(f => f.Slug == slug)
            .Select(f => f.Id)
            .SingleAsync();
    }

    private async Task<int> DerivedEdgeCountAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .EntryLinks
            .CountAsync(link => link.FromEntryId == entryId);
    }

    private async Task<List<DerivedRow>> DerivedRowsAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        return await db.EntryLinks
            .AsNoTracking()
            .Where(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Derived)
            .Select(link => new DerivedRow(link.ToKind.ToString().ToLowerInvariant(), link.ToId))
            .ToListAsync();
    }

    private async Task<EntryJson> GetDetailAsync(Guid id)
    {
        var detail = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{id}", JsonOptions);
        detail.ShouldNotBeNull();
        return detail!;
    }

    private async Task<List<string>> ResponsePropertyNames(string path)
    {
        var payload = await _client.GetFromJsonAsync<JsonElement>(path, JsonOptions);
        return [.. payload.EnumerateObject().Select(property => property.Name)];
    }

    private static async Task<ProblemJson> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        return problem!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record SessionJson(Guid Id);

    public sealed record ProblemJson(string? Title, string? Detail, int Status);

    public sealed record DerivedRow(string Kind, Guid Id);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label);

    public sealed record BacklinkJson(Guid EntryId, string Title, string Snippet);

    public sealed record EntryVersionJson(int Version, string Content, DateTime CreatedAt);

    public sealed record HitJson(Guid EntryId, string Title, string Snippet, double Rank);

    public sealed record SearchJson(string Query, List<HitJson> Hits);

    public sealed record EntryJson(
        Guid Id,
        string Type,
        string Title,
        string Content,
        int CurrentVersion,
        string State,
        string? AuthorAlias,
        List<OutgoingJson> Outgoing,
        List<BacklinkJson> Incoming,
        List<string> Unresolved);
}
