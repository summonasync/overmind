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

    [Fact]
    public async Task GetCounts_CountsLivingJournalNotesPerTagForTheProject()
    {
        await CreateEntryWithTags("alpha", "gamma");
        await CreateJournalEntryWithTags("alpha", "beta");
        var lifted = await CreateJournalEntryWithTags("beta", "delta");

        var lift = await _client.PostAsJsonAsync($"/api/entries/{lifted}/lift", new { Reason = "counts test lift" });
        lift.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await _client.GetFromJsonAsync<List<TagCountJson>>(
            $"/api/tags/counts?projectId={_projectId}",
            JsonOptions);

        counts.ShouldNotBeNull();
        // alpha lives on one active journal note; beta on the same note plus the lifted one
        // that no longer counts; gamma on a Note entry the journal definition ignores; delta
        // only on the lifted note, so its count dropped to zero.
        counts.Single(c => c.Name == "alpha").Count.ShouldBe(1);
        counts.Single(c => c.Name == "beta").Count.ShouldBe(1);
        counts.Single(c => c.Name == "gamma").Count.ShouldBe(0);
        counts.Single(c => c.Name == "delta").Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetCounts_SameTagInTwoProjects_CountsOnlyTheScopedProject()
    {
        var secondProject = CreateProject("tags-counts-second");
        await CreateJournalEntryWithTags("shared");
        await CreateJournalEntryIn(secondProject, "shared");

        var scoped = await _client.GetFromJsonAsync<List<TagCountJson>>(
            $"/api/tags/counts?projectId={_projectId}",
            JsonOptions);
        var away = await _client.GetFromJsonAsync<List<TagCountJson>>(
            $"/api/tags/counts?projectId={secondProject}",
            JsonOptions);

        scoped!.Single(c => c.Name == "shared").Count.ShouldBe(1);
        away!.Single(c => c.Name == "shared").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounts_UnknownProject_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/tags/counts?projectId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateJournalEntryWithTags(params string[] tags) =>
        await CreateJournalEntryIn(_projectId, tags);

    private async Task<Guid> CreateJournalEntryIn(Guid projectId, params string[] tags)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Journal",
            Title = "journal seed",
            Content = "journal seed content",
            Tags = tags,
            SkillFiles = (object[]?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatedEntryJson>(JsonOptions);
        return created!.Id;
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

    public sealed record CreatedEntryJson(Guid Id);

    public sealed record TagJson(Guid Id, string Name, string? Color);

    public sealed record TagCountJson(Guid TagId, string Name, int Count);
}
