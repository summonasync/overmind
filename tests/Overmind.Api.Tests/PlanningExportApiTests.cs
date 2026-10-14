using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningExportApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningExportApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-export-api-{Guid.NewGuid():N}");
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
    public async Task Export_AgentDoor_RendersCompleteSnapshotIntoEmptyTarget()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureWithTaskAsync(project.Id);
        await CreateEntryAsync(project.Id);
        var target = Path.Combine(_sandboxDir, "snapshot");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/export", new { TargetPath = target });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var export = await response.Content.ReadFromJsonAsync<ExportJson>(JsonOptions);
        export.ShouldNotBeNull();
        export!.ProjectId.ShouldBe(project.Id);
        export.Target.ShouldBe(Path.GetFullPath(target));
        export.FileCount.ShouldBeGreaterThan(0);

        Path.Exists(Path.Combine(target, "BACKLOG", "expfeat", "feature.md")).ShouldBeTrue();
        Path.Exists(Path.Combine(target, "BACKLOG", "expfeat", "tasks", "01-first-task.md")).ShouldBeTrue();
        Directory.EnumerateFiles(Path.Combine(target, "ENTRIES"), "*.md", SearchOption.AllDirectories)
            .ShouldNotBeEmpty();

        var status = await _client.GetFromJsonAsync<StatusJson>(
            $"/api/agent/projects/{project.Id}/planning/materialization-status", JsonOptions);
        status.ShouldNotBeNull();
        status!.TrackedFiles.ShouldBe(0);
    }

    [Fact]
    public async Task Export_HumanDoor_BehavesLikeTheAgentDoor()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureWithTaskAsync(project.Id);
        var target = Path.Combine(_sandboxDir, "snapshot");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/export", new { TargetPath = target });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var export = await response.Content.ReadFromJsonAsync<ExportJson>(JsonOptions);
        export.ShouldNotBeNull();
        export!.FileCount.ShouldBeGreaterThan(0);
        Path.Exists(Path.Combine(target, "BACKLOG", "expfeat", "feature.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_NonEmptyTarget_ReturnsTyped409()
    {
        var project = await CreateProjectAsync();
        var target = Path.Combine(_sandboxDir, "snapshot");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "keep.md"), "user file");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/export", new { TargetPath = target });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("ExportTargetNotEmpty");
        problem.Detail.ShouldNotBeNull();
        problem.Detail!.ShouldContain(target);
        File.Exists(Path.Combine(target, "keep.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_TargetInsideMirror_ReturnsTyped409()
    {
        var project = await CreateProjectAsync();
        var target = Path.Combine(_projectDir, "BACKLOG", "snapshot");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/export", new { TargetPath = target });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("ExportTargetInsideMirror");
    }

    [Fact]
    public async Task Export_RelativeTarget_ReturnsValidationProblem()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/export", new { TargetPath = "relative/snapshot" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("TargetPath");
    }

    [Fact]
    public async Task Export_UnknownProject_Returns404()
    {
        var projectId = Guid.NewGuid();
        var target = Path.Combine(_sandboxDir, "snapshot");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/export", new { TargetPath = target });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("Not Found");
        problem.Detail.ShouldNotBeNull();
        problem.Detail!.ShouldContain(projectId.ToString());
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"export-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();

        var workflow = await _client.PutAsJsonAsync(
            $"/api/projects/{project!.Id}/planning/workflow",
            new { Materialization = "off" });
        workflow.StatusCode.ShouldBe(HttpStatusCode.OK);

        return project;
    }

    private async Task CreateFeatureWithTaskAsync(Guid projectId)
    {
        var feature = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = "expfeat", Title = "Export Feature", Body = string.Empty });
        feature.StatusCode.ShouldBe(HttpStatusCode.Created);

        var task = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/expfeat/artifacts",
            new { Kind = "task", Body = "# First Task", Order = 1 });
        task.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task CreateEntryAsync(Guid projectId)
    {
        var entry = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = "export note",
            Content = "snapshot payload",
            Tags = (string[]?)null,
        });
        entry.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record ExportJson(Guid ProjectId, string Target, int FileCount);

    private sealed record StatusJson(int TrackedFiles);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
