using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryFiltersApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    public EntryFiltersApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("filters-main");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Create_AgentDoor_StampsSessionAlias()
    {
        var session = await RegisterSessionAsync("Van", "claude");
        var created = await CreateEntryAsync("agent note", "logged by the agent door", session.Id);

        created.AuthorAlias.ShouldBe("Van");
    }

    [Fact]
    public async Task Create_HumanDoor_LeavesAuthorAliasNull()
    {
        var created = await CreateEntryAsync("human note", "logged from the ui");

        created.AuthorAlias.ShouldBeNull();
    }

    [Fact]
    public async Task Create_UnregisteredSession_FallsBackToSessionId()
    {
        var unknownSessionId = Guid.NewGuid();
        var created = await CreateEntryAsync("stray session note", "session row is gone", unknownSessionId);

        created.AuthorAlias.ShouldBe(unknownSessionId.ToString());
    }

    [Fact]
    public async Task List_SinceUntil_NarrowsByCreatedAt()
    {
        await CreateEntryAsync("first entry", "alpha body");
        await CreateEntryAsync("second entry", "beta body");

        var windowed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?since=2000-01-01&until=2999-01-01", JsonOptions);
        windowed.ShouldNotBeNull();
        windowed!.Count.ShouldBe(2);

        var sinceInstant = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?since=1999-01-01T00:00:00Z", JsonOptions);
        sinceInstant.ShouldNotBeNull();
        sinceInstant!.Count.ShouldBe(2);

        var beforeAll = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?until=2000-01-02", JsonOptions);
        beforeAll.ShouldNotBeNull();
        beforeAll!.ShouldBeEmpty();

        var afterAll = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?since=2999-01-01", JsonOptions);
        afterAll.ShouldNotBeNull();
        afterAll!.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("since", "not-a-date")]
    [InlineData("until", "13/45/2020")]
    [InlineData("since", "yesterday")]
    public async Task List_InvalidDate_Returns400(string name, string value)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/entries?{name}={Uri.EscapeDataString(value)}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Author_MatchesCaseInsensitiveExact()
    {
        var vanSession = await RegisterSessionAsync("Van", "claude");
        var danSession = await RegisterSessionAsync("dan", "qwen");
        var vanEntry = await CreateEntryAsync("van note", "shared marker", vanSession.Id);
        var danEntry = await CreateEntryAsync("dan note", "shared marker", danSession.Id);
        var humanEntry = await CreateEntryAsync("human note", "shared marker");

        var vanQuery = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?author=van", JsonOptions);
        vanQuery.ShouldNotBeNull();
        vanQuery!.Select(e => e.Id).ShouldBe([vanEntry.Id]);

        var danQuery = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?author=DAN", JsonOptions);
        danQuery.ShouldNotBeNull();
        danQuery!.Select(e => e.Id).ShouldBe([danEntry.Id]);

        var everyone = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?since=2000-01-01", JsonOptions);
        everyone.ShouldNotBeNull();
        everyone!.Count.ShouldBe(3);
        everyone.ShouldContain(e => e.Id == humanEntry.Id);
    }

    [Fact]
    public async Task List_AuthorComposesWithStateAndDateFilters()
    {
        var vanSession = await RegisterSessionAsync("Van", "claude");
        var kept = await CreateEntryAsync("kept note", "composable body", vanSession.Id);
        var lifted = await CreateEntryAsync("lifted note", "composable body", vanSession.Id);
        await LiftAsync(lifted.Id);

        var allStates = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?author=van&state=all", JsonOptions);
        allStates.ShouldNotBeNull();
        allStates!.Select(e => e.Id).ShouldBe(new[] { lifted.Id, kept.Id }, ignoreOrder: true);

        var liftedOnly = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?author=van&state=lifted", JsonOptions);
        liftedOnly.ShouldNotBeNull();
        liftedOnly!.Single().Id.ShouldBe(lifted.Id);

        var emptyWindow = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?author=van&state=all&since=2999-01-01", JsonOptions);
        emptyWindow.ShouldNotBeNull();
        emptyWindow!.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_QueryPath_HonorsDateAndAuthorFilters()
    {
        var vanSession = await RegisterSessionAsync("Van", "claude");
        var vanEntry = await CreateEntryAsync("van quokka", "quokka lives here", vanSession.Id);
        await CreateEntryAsync("human quokka", "quokka lives here");

        var authored = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?q=quokka&author=van", JsonOptions);
        authored.ShouldNotBeNull();
        authored!.Single().Id.ShouldBe(vanEntry.Id);

        var dated = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?q=quokka&author=van&since=2999-01-01", JsonOptions);
        dated.ShouldNotBeNull();
        dated!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_HonorsAuthorAndDateFilters()
    {
        var vanSession = await RegisterSessionAsync("Van", "claude");
        await CreateEntryAsync("van wallaby", "wallaby hops alone", vanSession.Id);
        await CreateEntryAsync("human wallaby", "wallaby hops alone");

        var authored = await _client.GetFromJsonAsync<SearchJson>(
            $"/api/search?q=wallaby&projectId={_projectId}&author=van", JsonOptions);
        authored.ShouldNotBeNull();
        authored!.Hits.ShouldAllBe(h => h.Source == "entry");
        authored.Hits.Select(h => h.Title).ShouldBe(["van wallaby"]);

        var dated = await _client.GetFromJsonAsync<SearchJson>(
            $"/api/search?q=wallaby&projectId={_projectId}&author=van&since=2999-01-01", JsonOptions);
        dated.ShouldNotBeNull();
        dated!.Hits.ShouldBeEmpty();

        var unfiltered = await _client.GetFromJsonAsync<SearchJson>(
            $"/api/search?q=wallaby&projectId={_projectId}", JsonOptions);
        unfiltered.ShouldNotBeNull();
        unfiltered!.Hits.ShouldContain(h => h.Source == "entry");
    }

    private async Task LiftAsync(Guid entryId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/entries/{entryId}/lift", new { Reason = "replaced" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<SessionJson> RegisterSessionAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/sessions", new { Alias = alias, Vendor = vendor });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SessionJson>(JsonOptions))!;
    }

    private async Task<EntryJson> CreateEntryAsync(string title, string content, Guid? sessionId = null)
    {
        var body = new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        };

        HttpResponseMessage response;
        if (sessionId.HasValue)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/entries")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            request.Headers.Add("X-Overmind-Session", sessionId.Value.ToString());
            response = await _client.SendAsync(request);
        }
        else
        {
            response = await _client.PostAsJsonAsync("/api/entries", body, JsonOptions);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!;
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

    public sealed record SessionJson(Guid Id);

    public sealed record SearchJson(List<SearchHitJson> Hits);

    public sealed record SearchHitJson(string Source, string Title, string Snippet);

    public sealed record EntryJson(
        Guid Id,
        string Title,
        string Type,
        int CurrentVersion,
        List<string> Tags,
        string State,
        string? AuthorAlias = null);
}
