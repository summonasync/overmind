using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ProjectApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProjectApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Project_Lifecycle_Works()
    {
        var created = await CreateProjectAsync("alpha", TestPaths.DiskPath("alpha"));
        created.Id.ShouldNotBe(Guid.Empty);

        var listed = await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions);
        listed.ShouldNotBeNull();
        listed!.Count.ShouldBe(1);
        listed[0].Name.ShouldBe("alpha");
        listed[0].DiskPath.ShouldBe(TestPaths.DiskPath("alpha"));

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.Description.ShouldBe("first project");

        var update = new { Name = "alpha-renamed", DiskPath = TestPaths.DiskPath("alpha2"), Description = "renamed" };
        var putResponse = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", update);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("alpha-renamed");

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var missing = await _client.GetAsync($"/api/projects/{created.Id}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await missing.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(404);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await CreateProjectAsync("dup", TestPaths.DiskPath("dup"));

        var conflict = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "dup",
            DiskPath = TestPaths.DiskPath("other"),
            Description = (string?)null,
        });

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400WithValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "",
            DiskPath = TestPaths.DiskPath("x"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(400);
        problem.Errors.ShouldNotBeNull();
        problem.Errors!.ContainsKey("Name").ShouldBeTrue();
    }

    [Fact]
    public async Task Create_RelativeDiskPath_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "rel",
            DiskPath = "some/relative/path",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("DiskPath");
    }

    [Fact]
    public async Task Update_MissingProject_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/projects/{Guid.NewGuid()}", new
        {
            Name = "ghost",
            DiskPath = TestPaths.DiskPath("ghost"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Numbering_DefaultsToHashWithoutPrefix()
    {
        var created = await CreateProjectAsync("numbering-default", TestPaths.DiskPath("numbering-default"));

        created.NumberStyle.ShouldBe("hash");
        created.NumberPrefix.ShouldBeNull();
    }

    [Fact]
    public async Task Update_NumberStyleKeyWithPrefix_RoundTripsAndClearsOnStyleChange()
    {
        var created = await CreateProjectAsync("numbered", TestPaths.DiskPath("numbered"));

        var keyResponse = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "numbered",
            DiskPath = TestPaths.DiskPath("numbered"),
            NumberStyle = "key",
            NumberPrefix = "OM",
        });
        keyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var keyed = await keyResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        keyed!.NumberStyle.ShouldBe("key");
        keyed.NumberPrefix.ShouldBe("OM");

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched!.NumberStyle.ShouldBe("key");
        fetched.NumberPrefix.ShouldBe("OM");

        var plainResponse = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "numbered",
            DiskPath = TestPaths.DiskPath("numbered"),
            NumberStyle = "plain",
        });
        plainResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plain = await plainResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        plain!.NumberStyle.ShouldBe("plain");
        plain.NumberPrefix.ShouldBeNull();
    }

    [Fact]
    public async Task Update_KeyStyleWithoutPrefix_Returns400()
    {
        var created = await CreateProjectAsync("prefixless", TestPaths.DiskPath("prefixless"));

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "prefixless",
            DiskPath = TestPaths.DiskPath("prefixless"),
            NumberStyle = "key",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Errors.ShouldNotBeNull();
        problem.Errors!.ContainsKey("NumberPrefix").ShouldBeTrue();
    }

    [Fact]
    public async Task Update_PrefixWithoutKeyStyle_Returns400()
    {
        var created = await CreateProjectAsync("stray-prefix", TestPaths.DiskPath("stray-prefix"));

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "stray-prefix",
            DiskPath = TestPaths.DiskPath("stray-prefix"),
            NumberPrefix = "OM",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Errors.ShouldNotBeNull();
        problem.Errors!.ContainsKey("NumberPrefix").ShouldBeTrue();
    }

    [Theory]
    [InlineData("om")]
    [InlineData("O")]
    [InlineData("ABCDEFGHIJKLMNOPQ")]
    public async Task Update_InvalidPrefix_Returns400(string prefix)
    {
        var created = await CreateProjectAsync("bad-prefix", TestPaths.DiskPath("bad-prefix"));

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "bad-prefix",
            DiskPath = TestPaths.DiskPath("bad-prefix"),
            NumberStyle = "key",
            NumberPrefix = prefix,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Errors.ShouldNotBeNull();
        problem.Errors!.ContainsKey("NumberPrefix").ShouldBeTrue();
    }

    [Fact]
    public async Task Update_UntrimmedPrefix_TrimsBeforeValidation()
    {
        var created = await CreateProjectAsync("padded-prefix", TestPaths.DiskPath("padded-prefix"));

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "padded-prefix",
            DiskPath = TestPaths.DiskPath("padded-prefix"),
            NumberStyle = "key",
            NumberPrefix = " OM ",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated!.NumberPrefix.ShouldBe("OM");
    }

    [Fact]
    public async Task Update_UnknownNumberStyle_Returns400NamingAllowedValues()
    {
        var created = await CreateProjectAsync("bad-style", TestPaths.DiskPath("bad-style"));

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            Name = "bad-style",
            DiskPath = TestPaths.DiskPath("bad-style"),
            NumberStyle = "banana",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Errors.ShouldNotBeNull();
        problem.Errors!["NumberStyle"][0].ShouldContain("hash");
        problem.Errors["NumberStyle"][0].ShouldContain("key");
        problem.Errors["NumberStyle"][0].ShouldContain("plain");
    }

    [Fact]
    public async Task SetFavorite_ToggleRoundTrip_PersistsState()
    {
        var created = await CreateProjectAsync("favorite", TestPaths.DiskPath("favorite"));

        var starResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{created.Id}/favorite", new { IsFavorite = true });
        starResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var starred = await starResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        starred!.IsFavorite.ShouldBeTrue();

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched!.IsFavorite.ShouldBeTrue();

        var unstarResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{created.Id}/favorite", new { IsFavorite = false });
        var unstarred = await unstarResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        unstarred!.IsFavorite.ShouldBeFalse();
    }

    [Fact]
    public async Task SetFavorite_MissingProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/favorite", new { IsFavorite = true });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetAutoAct_ToggleRoundTrip_PersistsState()
    {
        var created = await CreateProjectAsync("auto-act", TestPaths.DiskPath("auto-act"));

        var enableResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{created.Id}/auto-act", new { Enabled = true });
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var enabled = await enableResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        enabled!.AutoActOnReplies.ShouldBeTrue();

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched!.AutoActOnReplies.ShouldBeTrue();

        var disableResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{created.Id}/auto-act", new { Enabled = false });
        var disabled = await disableResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        disabled!.AutoActOnReplies.ShouldBeFalse();
    }

    [Fact]
    public async Task SetAutoAct_MissingProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/auto-act", new { Enabled = true });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetAutoAct_NonBooleanBody_Returns400()
    {
        var created = await CreateProjectAsync("auto-act-junk", TestPaths.DiskPath("auto-act-junk"));

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{created.Id}/auto-act", new { Enabled = "yes" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetAutoAct_UntouchedProject_DefaultsToTrue()
    {
        var created = await CreateProjectAsync("auto-act-default", TestPaths.DiskPath("auto-act-default"));

        var fetched = await _client.GetFromJsonAsync<ProjectJson>($"/api/projects/{created.Id}", JsonOptions);
        fetched!.AutoActOnReplies.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAll_OpenFeatureCount_ExcludesDoneFeatures()
    {
        var created = await CreateProjectAsync("counted", TestPaths.DiskPath("counted"));
        await CreateFeatureAsync(created.Id, "open-one");
        await CreateFeatureAsync(created.Id, "closed-one");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            db.PlanningFeatures.Single(f => f.Slug == "closed-one").SetStatus(PlanningFeatureStatus.Done);
            db.SaveChanges();
        }

        var raw = await _client.GetStringAsync("/api/projects");
        using var document = JsonDocument.Parse(raw);
        var fieldNames = document.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();
        fieldNames.ShouldContain("isFavorite");
        fieldNames.ShouldContain("openFeatureCount");

        var listed = await _client.GetFromJsonAsync<List<ProjectJson>>("/api/projects", JsonOptions);
        listed!.Single().OpenFeatureCount.ShouldBe(1);
    }

    private async Task<ProjectJson> CreateProjectAsync(string name, string diskPath)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = diskPath,
            Description = "first project",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public sealed record ProjectJson(
        Guid Id,
        string Name,
        string DiskPath,
        string Description,
        string NumberStyle,
        string? NumberPrefix,
        bool IsFavorite,
        bool AutoActOnReplies,
        int OpenFeatureCount,
        DateTime CreatedAt);

    public sealed record ProblemJson(
        string? Title,
        int? Status,
        string? Detail,
        Dictionary<string, string[]>? Errors);
}
