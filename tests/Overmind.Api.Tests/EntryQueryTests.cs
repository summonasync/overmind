using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryQueryTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryQueryTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("query-main");
        _otherProjectId = CreateProject("query-other");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Query_FtsFindsRightEntry()
    {
        await CreateEntryAsync(_projectId, "Note", "plain note", "nothing special here");
        await CreateEntryAsync(_projectId, "Decision", "zebra decision", "we picked zebra as the mascot");
        await CreateEntryAsync(_otherProjectId, "Note", "other zebra", "zebra lives in another project");

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?q=zebra", JsonOptions);

        results.ShouldNotBeNull();
        results!.Count.ShouldBe(1);
        results[0].Title.ShouldBe("zebra decision");
    }

    [Fact]
    public async Task Query_TypeFilter_NarrowsResults()
    {
        await CreateEntryAsync(_projectId, "Note", "note-a", "commonmarker alpha");
        await CreateEntryAsync(_projectId, "Decision", "decision-a", "commonmarker beta");

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=Decision", JsonOptions);

        results.ShouldNotBeNull();
        results!.Count.ShouldBe(1);
        results[0].Title.ShouldBe("decision-a");
        results[0].Type.ShouldBe("decision");
    }

    [Fact]
    public async Task Query_TagFilter_NarrowsResults()
    {
        await CreateEntryAsync(_projectId, "Note", "tagged", "taggable content", "hot");
        await CreateEntryAsync(_projectId, "Note", "untagged", "more taggable content", "cold");

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?tag=hot", JsonOptions);

        results.ShouldNotBeNull();
        results!.Count.ShouldBe(1);
        results[0].Title.ShouldBe("tagged");
    }

    [Fact]
    public async Task Query_MissingProject_Returns404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/entries");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Query_TrioType_DefaultsToActiveOnly()
    {
        var activeGuard = await CreateEntryAsync(_projectId, "Guard", "active guard", "koalas stay awake");
        var liftedGate = await CreateEntryAsync(_projectId, "Gate", "lifted gate", "koalas sleep at noon");
        await LiftAsync(liftedGate.Id);

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=gate", JsonOptions);

        results.ShouldNotBeNull();
        results!.ShouldBeEmpty();

        var active = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=guard", JsonOptions);
        active.ShouldNotBeNull();
        active!.Select(e => e.Id).ShouldBe([activeGuard.Id]);
    }

    [Fact]
    public async Task Query_NonTrioType_DefaultsToAll()
    {
        var alive = await CreateEntryAsync(_projectId, "Note", "alive note", "plains of the serengeti");
        var lifted = await CreateEntryAsync(_projectId, "Note", "lifted note", "plains of the serengeti");
        await LiftAsync(lifted.Id);

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=note", JsonOptions);

        results.ShouldNotBeNull();
        results!.Select(e => e.Id).ShouldBe(new[] { lifted.Id, alive.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task Query_ExplicitLiftedState_ReturnsLiftedEntriesOnly()
    {
        var liftedGate = await CreateEntryAsync(_projectId, "Gate", "lifted gate", "gate content");
        await CreateEntryAsync(_projectId, "Gate", "active gate", "gate content");
        await LiftAsync(liftedGate.Id);

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=gate&state=lifted", JsonOptions);

        results.ShouldNotBeNull();
        results!.Single().Id.ShouldBe(liftedGate.Id);
        results.Single().State.ShouldBe("lifted");
    }

    [Fact]
    public async Task Query_ExplicitAllState_ReturnsEverything()
    {
        var gate = await CreateEntryAsync(_projectId, "Gate", "mixed gate", "gate content");
        await LiftAsync(gate.Id);

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?state=all", JsonOptions);

        results.ShouldNotBeNull();
        results!.ShouldNotBeEmpty();
        results.ShouldContain(e => e.Id == gate.Id);
    }

    [Fact]
    public async Task Query_UnknownState_Returns400()
    {
        var response = await _client.GetAsync($"/api/projects/{_projectId}/entries?state=archived");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_SearchHonorsTrioDefaultAndExplicitAll()
    {
        var gate = await CreateEntryAsync(_projectId, "Gate", "searched gate", "wombat corridor");
        await LiftAsync(gate.Id);

        var defaultSearch = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=gate&q=wombat", JsonOptions);
        defaultSearch.ShouldNotBeNull();
        defaultSearch!.ShouldBeEmpty();

        var allSearch = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=gate&q=wombat&state=all", JsonOptions);
        allSearch.ShouldNotBeNull();
        allSearch!.Single().Id.ShouldBe(gate.Id);
    }

    [Fact]
    public async Task Query_SupersededState_FiltersThroughSearchPath()
    {
        var old = await CreateEntryAsync(_projectId, "Decision", "old decision", "pelican routes");
        var next = await CreateEntryAsync(_projectId, "Decision", "new decision", "pelican routes");
        await LiftAsync(old.Id, next.Id);

        var results = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=decision&q=pelican", JsonOptions);

        results.ShouldNotBeNull();
        results!.Single().Id.ShouldBe(next.Id);
        results.Single().State.ShouldBe("active");

        var superseded = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=decision&q=pelican&state=superseded", JsonOptions);
        superseded.ShouldNotBeNull();
        superseded!.Single().Id.ShouldBe(old.Id);
    }

    private async Task LiftAsync(Guid entryId, Guid? supersededById = null)
    {
        object body = supersededById.HasValue
            ? new { Reason = "replaced", SupersededById = supersededById.Value }
            : new { Reason = "replaced" };
        var response = await _client.PostAsJsonAsync($"/api/entries/{entryId}/lift", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    private async Task<EntryJson> CreateEntryAsync(Guid projectId, string type, string title, string content, params string[] tags)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Content = content,
            Tags = tags.Length > 0 ? tags : null,
            SkillFiles = (object[]?)null,
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record EntryJson(
        Guid Id,
        string Title,
        string Type,
        int CurrentVersion,
        List<string> Tags,
        string State);
}
