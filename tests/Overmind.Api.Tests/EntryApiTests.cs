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
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone.ShouldNotBeNull();
        tombstone!.Type.ShouldBe("tombstone");

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
    public async Task Put_WithoutTags_OnTaggedEntry_KeepsTagsAndReturns200()
    {
        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "mcp update target",
            Content = "before",
            Tags = new[] { "alpha", "beta" },
            SkillFiles = (object[]?)null,
        });

        var putResponse = await _client.PutAsJsonAsync($"/api/entries/{created.Id}", new
        {
            Title = "mcp update target",
            Content = "after",
        });
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.Content.ShouldBe("after");
        updated.Tags.ShouldBe(["alpha", "beta"]);

        var fetched = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{created.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Tags.ShouldBe(["alpha", "beta"]);

        var versions = await _client.GetFromJsonAsync<List<VersionJson>>($"/api/entries/{created.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Count.ShouldBe(1);
        versions[0].Content.ShouldBe("before");
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

    [Fact]
    public async Task Create_CommaInTagName_Returns400WithoutCreatingEntry()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "comma tag",
            Content = "body",
            Tags = new[] { "alpha", "beta,gamma" },
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var listed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries", JsonOptions);
        listed.ShouldNotBeNull();
        listed!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_NullTagElement_Returns400Not500()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "null tag element",
            Content = "body",
            Tags = new string?[] { null },
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_CommaInTagName_Returns400AndKeepsExistingTags()
    {
        var entry = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "comma update target",
            Content = "before",
            Tags = new[] { "alpha" },
            SkillFiles = (object[]?)null,
        });

        var putResponse = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "comma update target",
            Content = "after",
            Tags = new[] { "alpha", "beta,gamma" },
        });
        putResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var unchanged = await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{entry.Id}", JsonOptions);
        unchanged.ShouldNotBeNull();
        unchanged!.Tags.ShouldBe(["alpha"]);
        unchanged.Content.ShouldBe("before");
    }

    [Fact]
    public async Task Delete_WithReason_ConvertsToTombstoneCarryingMemory()
    {
        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Decision",
            Title = "buried decision",
            Content = "the old way",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        var deleteResponse = await _client.DeleteAsync($"/api/entries/{created.Id}?reason=superseded%20by%20the%20new%20flow");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone.ShouldNotBeNull();
        tombstone!.Id.ShouldNotBe(created.Id);
        tombstone.Type.ShouldBe("tombstone");
        tombstone.Title.ShouldBe("buried decision (deleted)");
        tombstone.CurrentVersion.ShouldBe(0);
        tombstone.Content.ShouldContain("Reason: superseded by the new flow");
        tombstone.Content.ShouldContain("Original type: decision");
        tombstone.Content.ShouldContain("via human door.");
        tombstone.Content.ShouldContain("the old way");

        (await _client.GetAsync($"/api/entries/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/entries/{created.Id}/versions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/entries/{tombstone.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithoutReason_OmitsReasonLine()
    {
        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "quiet burial",
            Content = "plain body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        var deleteResponse = await _client.DeleteAsync($"/api/entries/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone.ShouldNotBeNull();
        tombstone!.Content.ShouldNotContain("Reason:");
        tombstone.Content.ShouldContain("Original type: note");
    }

    [Fact]
    public async Task Delete_AgentDoor_RecordsSessionActor()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/agent/sessions", new { Alias = "Van", Vendor = "claude" });
        sessionResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionJson>(JsonOptions);

        var created = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Guard",
            Title = "doomed guard",
            Content = "guard body",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/entries/{created.Id}?reason=cleanup");
        request.Headers.Add("X-Overmind-Session", session!.Id.ToString());
        var deleteResponse = await _client.SendAsync(request);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        tombstone.ShouldNotBeNull();
        tombstone!.Content.ShouldContain("via agent door by Van (claude).");
        tombstone.Content.ShouldNotContain("human door");
    }

    [Fact]
    public async Task Delete_ConvertedTombstone_IsFilterableAndSearchable()
    {
        await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "kept note",
            Content = "stays alive",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        var buried = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Gate",
            Title = "buried gate",
            Content = "wombats prefer dusk",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
        var deleteResponse = await _client.DeleteAsync($"/api/entries/{buried.Id}");
        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        var filtered = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=tombstone", JsonOptions);
        filtered.ShouldNotBeNull();
        filtered!.Count.ShouldBe(1);
        filtered[0].Id.ShouldBe(tombstone!.Id);
        filtered[0].Title.ShouldBe("buried gate (deleted)");

        var searched = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=tombstone&q=wombats", JsonOptions);
        searched.ShouldNotBeNull();
        searched!.Count.ShouldBe(1);
        searched[0].Id.ShouldBe(tombstone.Id);
    }

    [Fact]
    public async Task Delete_Tombstone_DeletesCleanlyWithoutSecondTombstone()
    {
        var buried = await CreateEntryAsync(new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = "twice buried",
            Content = "first the note, then the stone",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
        var firstDelete = await _client.DeleteAsync($"/api/entries/{buried.Id}");
        var tombstone = await firstDelete.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        var secondDelete = await _client.DeleteAsync($"/api/entries/{tombstone!.Id}");
        secondDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/entries/{tombstone.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var remaining = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{_projectId}/entries?type=tombstone", JsonOptions);
        remaining.ShouldNotBeNull();
        remaining!.ShouldBeEmpty();
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

    public sealed record SessionJson(Guid Id);

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
