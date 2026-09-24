using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class FeatureDependencyApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FeatureDependencyApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-dependencies-{Guid.NewGuid():N}");
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
    public async Task Add_ReturnsBothSidesOfTheLink()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "feat-b" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<DependenciesJson>(JsonOptions);
        view!.Slug.ShouldBe("feat-a");
        view.BlockedBy.Length.ShouldBe(1);
        view.BlockedBy[0].Slug.ShouldBe("feat-b");
        view.BlockedBy[0].Status.ShouldBe("draft");
        view.BlockedBy[0].Closed.ShouldBeFalse();
        view.Blocks.Length.ShouldBe(0);

        var otherSide = await _client.GetFromJsonAsync<DependenciesJson>(
            $"/api/projects/{project.Id}/planning/features/feat-b/dependencies", JsonOptions);
        otherSide!.Slug.ShouldBe("feat-b");
        otherSide.BlockedBy.Length.ShouldBe(0);
        otherSide.Blocks.Single().Slug.ShouldBe("feat-a");
        otherSide.Blocks.Single().Closed.ShouldBeFalse();
    }

    [Fact]
    public async Task Add_SameBlockerTwice_KeepsSingleEntry()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");

        await AddDependencyAsync(project.Id, "feat-a", "feat-b");
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "feat-b" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<DependenciesJson>(JsonOptions);
        view!.BlockedBy.Single().Slug.ShouldBe("feat-b");
    }

    [Fact]
    public async Task Add_DirectCycle_Returns409DependencyCycle()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-b/dependencies",
            new { BlockedBy = "feat-a" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("DependencyCycle");
        problem.Detail!.ShouldContain("dependency cycle");
    }

    [Fact]
    public async Task Add_ThreeFeatureCycle_Returns409DependencyCycle()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");
        await CreateFeature(project.Id, "feat-c", "Feature C");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");
        await AddDependencyAsync(project.Id, "feat-b", "feat-c");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-c/dependencies",
            new { BlockedBy = "feat-a" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("DependencyCycle");
        problem.Detail!.ShouldContain("feat-c -> feat-a -> feat-b -> feat-c");
    }

    [Fact]
    public async Task Add_SelfDependency_Returns409DependencyCycle()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "feat-a" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("DependencyCycle");
    }

    [Fact]
    public async Task Add_UnknownBlocker_Returns404MissingFeature()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "nope" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("Not Found");
        problem.Detail!.ShouldContain("'nope' was not found");
    }

    [Fact]
    public async Task Add_EmptyBlockedBy_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("BlockedBy");
    }

    [Fact]
    public async Task Remove_DeletesLinkAndClearsBlockedFlag()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");

        var boardBefore = await GetBoardAsync(project.Id);
        boardBefore!.Single(f => f.Slug == "feat-a").IsBlocked.ShouldBeTrue();

        var response = await _client.DeleteAsync(
            $"/api/projects/{project.Id}/planning/features/feat-a/dependencies/feat-b");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<DependenciesJson>(JsonOptions);
        view!.BlockedBy.Length.ShouldBe(0);
        view.Blocks.Length.ShouldBe(0);

        var boardAfter = await GetBoardAsync(project.Id);
        boardAfter!.Single(f => f.Slug == "feat-a").IsBlocked.ShouldBeFalse();

        var otherSide = await _client.GetFromJsonAsync<DependenciesJson>(
            $"/api/projects/{project.Id}/planning/features/feat-b/dependencies", JsonOptions);
        otherSide!.Blocks.Length.ShouldBe(0);
    }

    [Fact]
    public async Task List_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{project.Id}/planning/features/nonexistent/dependencies");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/planning/features/any/dependencies");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FeatureDetail_CarriesDependencyState()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");

        var blocked = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/feat-a", JsonOptions);
        blocked!.IsBlocked.ShouldBeTrue();
        blocked.BlockedBy.Single().Slug.ShouldBe("feat-b");
        blocked.BlockedBy.Single().Closed.ShouldBeFalse();
        blocked.Blocks.Length.ShouldBe(0);

        var blocker = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/feat-b", JsonOptions);
        blocker!.IsBlocked.ShouldBeFalse();
        blocker.BlockedBy.Length.ShouldBe(0);
        blocker.Blocks.Single().Slug.ShouldBe("feat-a");
    }

    [Fact]
    public async Task Board_IsBlockedOnlyWhileBlockerIsOpen()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");
        await AddDependencyAsync(project.Id, "feat-a", "feat-b");

        var openBoard = await GetBoardAsync(project.Id);
        openBoard!.Single(f => f.Slug == "feat-a").IsBlocked.ShouldBeTrue();

        await BringToStatusAsync(project.Id, "feat-b", "review");

        var closedBoard = await GetBoardAsync(project.Id);
        closedBoard!.Single(f => f.Slug == "feat-a").IsBlocked.ShouldBeFalse();

        var detail = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/feat-a", JsonOptions);
        detail!.BlockedBy.Single().Slug.ShouldBe("feat-b");
        detail.BlockedBy.Single().Closed.ShouldBeTrue();
        detail.IsBlocked.ShouldBeFalse();
    }

    [Fact]
    public async Task AgentDoor_AddListRemove_WorkUnderAgentRoutes()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "feat-a", "Feature A");
        await CreateFeature(project.Id, "feat-b", "Feature B");

        var add = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat-a/dependencies",
            new { BlockedBy = "feat-b" });
        add.StatusCode.ShouldBe(HttpStatusCode.OK);

        var view = await _client.GetFromJsonAsync<DependenciesJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat-a/dependencies", JsonOptions);
        view!.BlockedBy.Single().Slug.ShouldBe("feat-b");

        var remove = await _client.DeleteAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat-a/dependencies/feat-b");
        remove.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cleared = await remove.Content.ReadFromJsonAsync<DependenciesJson>(JsonOptions);
        cleared!.BlockedBy.Length.ShouldBe(0);
    }

    private async Task<HttpResponseMessage> AddDependencyAsync(Guid projectId, string slug, string blockedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/dependencies",
            new { BlockedBy = blockedBy });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return response;
    }

    private async Task<BoardItemJson[]?> GetBoardAsync(Guid projectId) =>
        await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{projectId}/planning/features", JsonOptions);

    private async Task BringToStatusAsync(Guid projectId, string slug, string target)
    {
        string[] path = target switch
        {
            "inProgress" => ["inProgress"],
            "review" => ["inProgress", "review"],
            "done" => ["inProgress", "review", "done"],
            _ => throw new ArgumentException($"unknown status '{target}'"),
        };

        foreach (var step in path)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}",
                new { Status = step });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"dependencies-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeature(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record BoardItemJson(
        string Slug, string Title, string Status, string Priority, int Version,
        string? SpecStatus, string? PlanStatus, int TaskTotal, int TaskDone, DateTime UpdatedAt,
        bool IsCurrent, bool IsBlocked);

    public sealed record FeatureJson(
        string Slug, string Title, string Body, string Status, string Priority,
        int Version, DateTime CreatedAt, DateTime UpdatedAt, ArtifactJson[] Artifacts,
        bool IsBlocked, EntryJson[] BlockedBy, EntryJson[] Blocks);

    public sealed record ArtifactJson(Guid Id);

    public sealed record DependenciesJson(string Slug, EntryJson[] BlockedBy, EntryJson[] Blocks);

    public sealed record EntryJson(string Slug, string Status, bool Closed);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);

    public sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
