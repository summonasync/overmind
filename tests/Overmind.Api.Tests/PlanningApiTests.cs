using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-planning-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Board_EmptyProject_ReturnsEmptyArray()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{project.Id}/planning/features", JsonOptions);

        response.ShouldNotBeNull();
        response!.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Board_WithFeatures_ReturnsCounters()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-1", "Feature 1");

        var artifactResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-1/artifacts",
            new { Kind = "task", Body = "task 1" });
        artifactResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var board = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{project.Id}/planning/features", JsonOptions);

        board.ShouldNotBeNull();
        board!.Length.ShouldBe(1);
        board[0].Slug.ShouldBe("feat-1");
        board[0].TaskTotal.ShouldBe(1);
        board[0].TaskDone.ShouldBe(0);
    }

    [Fact]
    public async Task CreateFeature_Returns201()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features",
            new { Slug = "my-feature", Title = "My Feature", Body = "# Feature" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        feature!.Slug.ShouldBe("my-feature");
        feature.Version.ShouldBe(1);
    }

    [Fact]
    public async Task CreateFeature_DuplicateSlug_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "dup", "First");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features",
            new { Slug = "dup", Title = "Second", Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("DuplicateSlug");
    }

    [Fact]
    public async Task CreateFeature_BadSlug_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features",
            new { Slug = "INVALID SLUG", Title = "Title", Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidSlug");
    }

    [Fact]
    public async Task GetFeature_UnknownSlug_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/planning/features/nonexistent");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeature_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/planning/features/any");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFeature_WithArtifacts_ReturnsOrdered()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task 1" });
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "# Spec" });

        var response = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/feat", JsonOptions);

        response.ShouldNotBeNull();
        response!.Artifacts.Length.ShouldBe(2);
        response.Artifacts[0].Kind.ShouldBe("spec");
        response.Artifacts[1].Kind.ShouldBe("task");
    }

    [Fact]
    public async Task UpdateFeature_DraftStatus_NoVersionBump()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Original");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Title = "Updated" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        feature!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateFeature_LeavingDraft_BumpsVersion()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Original");

        await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Status = "ready" });

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat",
            new { Title = "Updated" });

        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        feature!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ArtifactUpsert_Created_Returns201()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "# Spec" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        artifact!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task ArtifactUpsert_ExistingVersions_Returns200()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "v1" });

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "v2" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        artifact!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task ArtifactPut_BodyChange_IncrementsVersion()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "v1" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/planning/artifacts/{created!.Id}",
            new { Body = "v2" });

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Version.ShouldBe(2);

        var versions = await _client.GetFromJsonAsync<VersionJson[]>(
            $"/api/planning/artifacts/{created.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Length.ShouldBe(1);
        versions[0].Version.ShouldBe(1);
        versions[0].Body.ShouldBe("v1");
    }

    [Fact]
    public async Task ArtifactResponse_LogProjection_ParsedDates()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "body" });

        var updateResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "body v2", LogNote = "initial note" });
        var artifact = await updateResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        artifact.ShouldNotBeNull();
        artifact!.Log.Length.ShouldBe(1);
        artifact.Log[0].Date.ShouldNotBeNull();
        artifact.Log[0].Text.ShouldBe("initial note");
    }

    [Fact]
    public async Task ValidateSpec_SetsValidatedAndFrozenAt()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "# Spec" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var validateResponse = await _client.PostAsync(
            $"/api/planning/artifacts/{created!.Id}/validate", null);
        validateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{created.Id}", JsonOptions);
        getResponse!.Status.ShouldBe("validated");
        getResponse.FrozenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateTask_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var response = await _client.PostAsync(
            $"/api/planning/artifacts/{created!.Id}/validate", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("CannotValidateTask");
    }

    [Fact]
    public async Task FrozenEdit_WithoutLogNote_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "v1" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        await _client.PostAsync($"/api/planning/artifacts/{created!.Id}/validate", null);

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/planning/artifacts/{created.Id}",
            new { Body = "v2" });

        putResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await putResponse.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ChangeNoteRequired");
    }

    [Fact]
    public async Task FrozenEdit_WithLogNote_ResetsToDraft()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "v1" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        await _client.PostAsync($"/api/planning/artifacts/{created!.Id}/validate", null);

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/planning/artifacts/{created.Id}",
            new { Body = "v2", LogNote = "unfreezing" });

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Status.ShouldBe("draft");
        updated.FrozenAt.ShouldBeNull();
        updated.Log.Length.ShouldBe(1);
        updated.Log[0].Text.ShouldBe("unfreezing");
    }

    [Fact]
    public async Task SetStatus_WithNote_Persisted()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var statusResponse = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{created!.Id}/status",
            new { Status = "inProgress", LogNote = "started work" });

        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{created.Id}", JsonOptions);
        getResponse!.Status.ShouldBe("inProgress");
    }

    [Fact]
    public async Task SetStatus_SpecToDone_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "spec" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var response = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{created!.Id}/status",
            new { Status = "done", LogNote = "try done" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("InvalidStatusForKind");
    }

    [Fact]
    public async Task AppendLog_BodyUpdated_VersionUnchanged()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "body" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var logResponse = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{created!.Id}/log",
            new { Note = "a log note" });

        logResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{created.Id}", JsonOptions);
        getResponse!.Version.ShouldBe(created.Version);
        getResponse.Body.ShouldContain("a log note");
    }

    [Fact]
    public async Task DeleteArtifact_Returns204Then404()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "spec", Body = "spec" });
        var created = await createResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/planning/artifacts/{created!.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/planning/artifacts/{created.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFeature_Returns204Then404()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat", "Feature");

        var deleteResponse = await _client.DeleteAsync(
            $"/api/projects/{project.Id}/planning/features/feat");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync(
            $"/api/projects/{project.Id}/planning/features/feat");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetArtifact_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/api/planning/artifacts/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"planning-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<FeatureJson> CreateFeature(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        return feature!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record BoardItemJson(
        string Slug, string Title, string Status, int Version,
        string? SpecStatus, string? PlanStatus, int TaskTotal, int TaskDone, DateTime UpdatedAt);

    public sealed record FeatureJson(
        string Slug, string Title, string Body, string Status,
        int Version, DateTime CreatedAt, DateTime UpdatedAt, ArtifactJson[] Artifacts);

    public sealed record ArtifactJson(
        Guid Id, string Kind, string Status, string Review,
        int Order, string Body, DateTime? FrozenAt, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, LogLineJson[] Log);

    public sealed record LogLineJson(string? Date, string Text);

    public sealed record VersionJson(int Version, string Body, DateTime CreatedAt);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
