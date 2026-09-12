using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("entries-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Entry_Lifecycle_Works()
    {
        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "first note",
            Content = "hello world",
            Tags = new[] { "alpha", "beta" },
            SkillFiles = (object[]?)null,
        });

        created.Id.ShouldNotBe(Guid.Empty);
        created.CurrentVersion.ShouldBe(0);
        created.Tags.ShouldBe(["alpha", "beta"]);

        var fetched = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{created.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Content.ShouldBe("hello world");

        var update = new { Title = "first note v2", Content = "updated body", Tags = new[] { "gamma" }, SkillFiles = (object[]?)null };
        var putResponse = await _client.PutAsJsonAsync($"/api/entries/{created.Id}", update);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.Title.ShouldBe("first note v2");
        updated.CurrentVersion.ShouldBe(1);
        updated.Tags.ShouldBe(["gamma"]);

        var deleteResponse = await _client.DeleteAsync($"/api/entries/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var missing = await _client.GetAsync($"/api/entries/{created.Id}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_AppendsVersion_AndVersionsEndpointReturnsOldContent()
    {
        var entry = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "versioned",
            Content = "original text",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "versioned",
            Content = "replaced text",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        var versions = await _client.GetFromJsonAsync<List<VersionJson>>($"/api/entries/{entry.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Count.ShouldBe(1);
        versions[0].Version.ShouldBe(1);
        versions[0].Content.ShouldBe("original text");
    }

    [Fact]
    public async Task SkillEntry_RoundTripsFilesInline()
    {
        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Skill",
            Title = "reviewer skill",
            Content = "skill instructions",
            Tags = (string[]?)null,
            SkillFiles = new[]
            {
                new { Platform = "Claude", RelativePath = "SKILL.md", Content = "# Reviewer" },
                new { Platform = "KimiCode", RelativePath = ".kimi/reviewer.md", Content = "# Reviewer (Kimi)" },
            },
        });

        created.Type.ShouldBe("skill");
        created.SkillFiles.Count.ShouldBe(2);
        created.SkillFiles.Select(f => f.RelativePath).OrderBy(p => p).ShouldBe([".kimi/reviewer.md", "SKILL.md"]);

        var fetched = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{created.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.SkillFiles.Count.ShouldBe(2);
        fetched.SkillFiles.ShouldContain(f => f.Platform == "claude" && f.Content == "# Reviewer");
    }

    [Fact]
    public async Task SkillEntry_WithoutFiles_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Skill",
            Title = "broken skill",
            Content = "instructions",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InMissingProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = Guid.NewGuid(),
            Type = "Note",
            Title = "orphan",
            Content = "body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_EmptyContent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "empty",
            Content = "",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

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

    private async Task<EntryJson> CreateEntryAsync<T>(T body)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        entry.ShouldNotBeNull();
        return entry!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record SkillFileJson(string Platform, string RelativePath, string Content);

    public sealed record EntryJson(
        Guid Id,
        Guid ProjectId,
        string Type,
        string Title,
        string Content,
        int CurrentVersion,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        List<string> Tags,
        List<SkillFileJson> SkillFiles);

    public sealed record VersionJson(int Version, string Content, DateTime CreatedAt);
}
