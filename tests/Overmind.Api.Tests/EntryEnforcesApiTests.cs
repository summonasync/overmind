using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryEnforcesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryEnforcesApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("enforces-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Create_DecisionWithEnforces_PersistsTrue()
    {
        var entry = await CreateEntryAsync("Decision", enforces: true);
        entry.Enforces.ShouldBeTrue();

        var fetched = await _client.GetFromJsonAsync<EnforcedEntryJson>($"/api/entries/{entry.Id}", JsonOptions);
        fetched!.Enforces.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_DecisionWithoutEnforces_DefaultsFalse()
    {
        var entry = await CreateEntryAsync("Decision", enforces: null);
        entry.Enforces.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_NoteWithEnforcesTrue_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "enforcing note",
            Content = "body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
            Enforces = true,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_NoteWithEnforcesFalse_Succeeds()
    {
        var entry = await CreateEntryAsync("Note", enforces: false);
        entry.Enforces.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_DecisionEnforcesTrue_PersistsAndSurvivesFlaglessUpdate()
    {
        var entry = await CreateEntryAsync("Decision", enforces: null);

        var setResponse = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "decision",
            Content = "body",
            Enforces = true,
        });
        setResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await setResponse.Content.ReadFromJsonAsync<EnforcedEntryJson>(JsonOptions))!.Enforces.ShouldBeTrue();

        var keepResponse = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "decision",
            Content = "body v2",
        });
        keepResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await keepResponse.Content.ReadFromJsonAsync<EnforcedEntryJson>(JsonOptions))!.Enforces.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_DecisionEnforcesFalse_ClearsFlag()
    {
        var entry = await CreateEntryAsync("Decision", enforces: true);

        var response = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "decision",
            Content = "body",
            Enforces = false,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<EnforcedEntryJson>(JsonOptions))!.Enforces.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_NoteEnforcesTrue_Returns400AndKeepsFalse()
    {
        var entry = await CreateEntryAsync("Note", enforces: null);

        var response = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "note",
            Content = "body",
            Enforces = true,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var fetched = await _client.GetFromJsonAsync<EnforcedEntryJson>($"/api/entries/{entry.Id}", JsonOptions);
        fetched!.Enforces.ShouldBeFalse();
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

    private async Task<EnforcedEntryJson> CreateEntryAsync(string type, bool? enforces)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = type,
            Title = type.ToLowerInvariant(),
            Content = "body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
            Enforces = enforces,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EnforcedEntryJson>(JsonOptions))!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record EnforcedEntryJson(Guid Id, string Type, bool Enforces);
}
