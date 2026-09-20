using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class TagApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TagApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("tags-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsTagsSortedByNameWithNullColor()
    {
        await CreateEntryWithTags("zulu", "alpha");

        var tags = await _client.GetFromJsonAsync<List<TagJson>>("/api/tags", JsonOptions);

        tags.ShouldNotBeNull();
        tags.Count.ShouldBe(2);
        tags.Select(t => t.Name).ShouldBe(["alpha", "zulu"]);
        tags.ShouldAllBe(t => t.Id != Guid.Empty);
        tags.ShouldAllBe(t => t.Color == null);
    }

    [Fact]
    public async Task Put_SetClearAndUntouchedLifecycle()
    {
        await CreateEntryWithTags("alpha");
        var tag = await GetTagByName("alpha");

        var set = await PutColor(tag.Id, "#3B82F6");
        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var setBody = await set.Content.ReadFromJsonAsync<TagJson>(JsonOptions);
        setBody!.Color.ShouldBe("#3B82F6");

        (await GetTagByName("alpha")).Color.ShouldBe("#3B82F6");

        var untouched = await _client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { });
        untouched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetTagByName("alpha")).Color.ShouldBe("#3B82F6");

        var lowercase = await PutColor(tag.Id, "#a1b2c3");
        lowercase.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cleared = await PutColor(tag.Id, "");
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetTagByName("alpha")).Color.ShouldBeNull();
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#GGGGGG")]
    [InlineData("3B82F6")]
    [InlineData("#3B82F")]
    [InlineData("#3B82F6\n")]
    public async Task Put_RejectsInvalidColorFormat(string color)
    {
        await CreateEntryWithTags("alpha");
        var tag = await GetTagByName("alpha");

        var response = await PutColor(tag.Id, color);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetTagByName("alpha")).Color.ShouldBeNull();
    }

    [Fact]
    public async Task Put_UnknownTagId_ReturnsNotFound()
    {
        var response = await PutColor(Guid.NewGuid(), "#3B82F6");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task CreateEntryWithTags(params string[] tags)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "tag seed",
            Content = "seed content",
            Tags = tags,
            SkillFiles = (object[]?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<TagJson> GetTagByName(string name)
    {
        var tags = await _client.GetFromJsonAsync<List<TagJson>>("/api/tags", JsonOptions);
        return tags!.Single(t => t.Name == name);
    }

    private Task<HttpResponseMessage> PutColor(Guid id, string color) =>
        _client.PutAsJsonAsync($"/api/tags/{id}", new { Color = color });

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        var doc = response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult();
        return doc!.Id;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record TagJson(Guid Id, string Name, string? Color);
}
