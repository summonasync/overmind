using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SearchApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;
    private readonly Guid _otherProjectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SearchApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("search-main");
        _otherProjectId = CreateProject("search-other");

        CreateEntryAsync(_projectId, "giraffe notes", "the giraffe has a long neck").GetAwaiter().GetResult();
        CreateEntryAsync(_otherProjectId, "giraffe elsewhere", "another giraffe record").GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Search_ReturnsHitsAcrossProjects()
    {
        var result = await _client.GetFromJsonAsync<SearchJson>("/api/search?q=giraffe", JsonOptions);
        result.ShouldNotBeNull();
        result!.Hits.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Search_ProjectFilter_ScopesHits()
    {
        var result = await _client.GetFromJsonAsync<SearchJson>(
            $"/api/search?q=giraffe&projectId={_projectId}", JsonOptions);

        result.ShouldNotBeNull();
        result!.Hits.Count.ShouldBe(1);
        result.Hits[0].Title.ShouldBe("giraffe notes");
    }

    [Fact]
    public async Task Search_Metacharacters_ReturnEmptyInsteadOfError()
    {
        var response = await _client.GetAsync("/api/search?q=%22%28%28broken");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchJson>(JsonOptions);
        result.ShouldNotBeNull();
        result!.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_MissingQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/search");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = "E:\\work\\" + name,
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult()!.Id;
    }

    private async Task CreateEntryAsync(Guid projectId, string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.EnsureSuccessStatusCode();
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record HitJson(Guid EntryId, string Title, string Snippet, double Rank);

    public sealed record SearchJson(string Query, List<HitJson> Hits);
}
