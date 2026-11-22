using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryLifecycleApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryLifecycleApiTests()
    {
        _client = _factory.CreateClient();
        _projectId = CreateProject("lifecycle-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Lift_GuardOnHumanDoor_RecordsLiftedProvenance()
    {
        var entry = await CreateEntryAsync("Guard", "quota guard", "stay under 80 percent");

        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new
        {
            Reason = "quota retired",
            Proof = "slack thread 42",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var lifted = await response.Content.ReadFromJsonAsync<LifecycleJson>(JsonOptions);
        lifted.ShouldNotBeNull();
        lifted!.State.ShouldBe("lifted");
        lifted.StateReason.ShouldBe("quota retired");
        lifted.StateProof.ShouldBe("slack thread 42");
        lifted.StateBy.ShouldBeNull();
        lifted.StateAt.ShouldNotBeNull();
        lifted.SupersededById.ShouldBeNull();
        lifted.Enforces.ShouldBeFalse();
    }

    [Fact]
    public async Task Lift_AgentSession_RecordsAliasAsStateBy()
    {
        var session = await CreateSessionAsync("Van", "claude");
        var entry = await CreateEntryAsync("Decision", "storage call", "sqlite everywhere");

        var lifted = await LiftAsync(entry.Id, new { Reason = "replaced by postgres" }, session);
        lifted.StateBy.ShouldBe("Van (claude)");
        lifted.State.ShouldBe("lifted");
    }

    [Fact]
    public async Task Lift_LeavesContentAndVersionsUntouched()
    {
        var entry = await CreateEntryAsync("Note", "quiet note", "body stays");

        var lifted = await LiftAsync(entry.Id, new { Reason = "noted as done" });
        lifted.Content.ShouldBe("body stays");
        lifted.CurrentVersion.ShouldBe(0);

        var versions = await _client.GetFromJsonAsync<List<VersionJson>>($"/api/entries/{entry.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Lift_UnknownEntry_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/entries/{Guid.NewGuid()}/lift", new { Reason = "gone" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lift_EmptyReason_Returns400()
    {
        var entry = await CreateEntryAsync("Note", "blank reason target", "body");
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new { Reason = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_SkillEntry_Returns400()
    {
        var entry = await CreateSkillAsync();
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new { Reason = "skills outlive rules" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_TombstoneEntry_Returns400()
    {
        var entry = await CreateEntryAsync("Note", "doomed note", "body");
        var deleteResponse = await _client.DeleteAsync($"/api/entries/{entry.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tombstone = await deleteResponse.Content.ReadFromJsonAsync<LifecycleJson>(JsonOptions);

        var response = await _client.PostAsJsonAsync($"/api/entries/{tombstone!.Id}/lift", new { Reason = "graves stay shut" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_SupersedeSibling_RecordsSupersededState()
    {
        var old = await CreateEntryAsync("Decision", "old routing", "direct calls everywhere");
        var next = await CreateEntryAsync("Decision", "new routing", "bff proxy only");

        var lifted = await LiftAsync(old.Id, new { Reason = "routing moved on", SupersededById = next.Id });
        lifted.State.ShouldBe("superseded");
        lifted.SupersededById.ShouldBe(next.Id);
        lifted.StateBy.ShouldBeNull();
    }

    [Fact]
    public async Task Lift_SupersedeSelf_Returns400()
    {
        var entry = await CreateEntryAsync("Note", "self loop", "body");
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new
        {
            Reason = "loop",
            SupersededById = entry.Id,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_SupersedeUnknownTarget_Returns400()
    {
        var entry = await CreateEntryAsync("Note", "missing target", "body");
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new
        {
            Reason = "ghost target",
            SupersededById = Guid.NewGuid(),
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_SupersedeTombstoneTarget_Returns400()
    {
        var buried = await CreateEntryAsync("Note", "buried target", "body");
        await _client.DeleteAsync($"/api/entries/{buried.Id}");
        var entry = await CreateEntryAsync("Note", "living entry", "body");

        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new
        {
            Reason = "points at a grave",
            SupersededById = buried.Id,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lift_SupersedeCrossProjectTarget_Returns400()
    {
        var otherProject = CreateProject("lifecycle-other");
        var outsider = await CreateEntryAsync(otherProject, "Note", "outsider", "foreign body");
        var entry = await CreateEntryAsync("Note", "local entry", "body");

        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/lift", new
        {
            Reason = "crosses the border",
            SupersededById = outsider.Id,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Append_GainsDatedFollowUpBlockWithNewVersion()
    {
        var entry = await CreateEntryAsync("Note", "followed note", "original body");

        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/append", new
        {
            Text = "QA asked for a repro video.",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var appended = await response.Content.ReadFromJsonAsync<LifecycleJson>(JsonOptions);
        appended.ShouldNotBeNull();
        appended!.CurrentVersion.ShouldBe(1);
        appended.Content.ShouldStartWith("original body");
        appended.Content.ShouldContain($"## Follow-up {DateTime.UtcNow:yyyy-MM-dd}");
        appended.Content.ShouldContain("QA asked for a repro video.");

        var versions = await _client.GetFromJsonAsync<List<VersionJson>>($"/api/entries/{entry.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Single().Version.ShouldBe(1);
        versions.Single().Content.ShouldBe("original body");
    }

    [Fact]
    public async Task Append_SecondCall_StacksBlocks()
    {
        var entry = await CreateEntryAsync("Note", "twice followed", "body");
        await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/append", new { Text = "first" });
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/append", new { Text = "second" });

        var appended = await response.Content.ReadFromJsonAsync<LifecycleJson>(JsonOptions);
        appended.ShouldNotBeNull();
        appended!.CurrentVersion.ShouldBe(2);
        appended.Content.ShouldContain("first");
        appended.Content.ShouldContain("second");
    }

    [Fact]
    public async Task Append_UnknownEntry_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/entries/{Guid.NewGuid()}/append", new { Text = "hello" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    public async Task Append_BadTextLength_Returns400(int length)
    {
        var entry = await CreateEntryAsync("Note", "bad append", "body");
        var response = await _client.PostAsJsonAsync($"/api/entries/{entry.Id}/append", new { Text = new string('x', length) });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(2001, 2001)]
    [InlineData(2441, 2400)]
    public async Task Append_OverCap_IsAcceptedWithWarnings(int sent, int stored)
    {
        // 266: follow-up text softened at the door — band stores whole, past-ceiling truncates
        // to 2400, both answer with the frozen warnings[] shape.
        var entry = await CreateEntryAsync("Note", "soft append", "body");
        var response = await _client.PostAsJsonAsync(
            $"/api/entries/{entry.Id}/append", new { Text = new string('x', sent) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var warning = payload.RootElement.GetProperty("warnings")[0];
        warning.GetProperty("accepted_len").GetInt32().ShouldBe(stored);
        warning.GetProperty("limit").GetInt32().ShouldBe(2000);
        warning.GetProperty("truncated").GetBoolean().ShouldBe(sent > 2440);
        payload.RootElement.GetProperty("content").GetString()!.ShouldContain(new string('x', stored));
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

    private async Task<Guid> CreateSessionAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/sessions", new { Alias = alias, Vendor = vendor });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SessionJson>(JsonOptions))!.Id;
    }

    private async Task<EntryRefJson> CreateEntryAsync(string type, string title, string content) =>
        await CreateEntryAsync(_projectId, type, title, content);

    private async Task<EntryRefJson> CreateEntryAsync(Guid projectId, string type, string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Content = content,
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EntryRefJson>(JsonOptions))!;
    }

    private async Task<EntryRefJson> CreateSkillAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Skill",
            Title = "reviewer skill",
            Content = "skill instructions",
            Tags = (string[]?)null,
            SkillFiles = new[] { new { Platform = "Claude", RelativePath = "SKILL.md", Content = "# Reviewer" } },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EntryRefJson>(JsonOptions))!;
    }

    private async Task<LifecycleJson> LiftAsync(Guid entryId, object body, Guid? sessionId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/entries/{entryId}/lift")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        if (sessionId.HasValue)
        {
            request.Headers.Add("X-Overmind-Session", sessionId.Value.ToString());
        }

        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<LifecycleJson>(JsonOptions))!;
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record SessionJson(Guid Id);

    public sealed record EntryRefJson(Guid Id);

    public sealed record VersionJson(int Version, string Content, DateTime CreatedAt);

    public sealed record LifecycleJson(
        Guid Id,
        string Type,
        string Content,
        int CurrentVersion,
        string State,
        DateTime? StateAt,
        string? StateBy,
        string? StateReason,
        string? StateProof,
        Guid? SupersededById,
        bool Enforces);
}
