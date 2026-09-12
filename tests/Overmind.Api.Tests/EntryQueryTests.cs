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

    private async Task CreateEntryAsync(Guid projectId, string type, string title, string content, params string[] tags)
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
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record EntryJson(
        Guid Id,
        string Title,
        string Type,
        int CurrentVersion,
        List<string> Tags);
}
