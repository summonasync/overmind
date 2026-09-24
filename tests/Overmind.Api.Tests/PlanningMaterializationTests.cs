using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningMaterializationTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningMaterializationTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-mat-{Guid.NewGuid():N}");
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
    public async Task Materialize_WithoutForce_SkipsForeignFileWritesTheRest()
    {
        var project = await CreateProjectWithMaterializationAsync("on");

        var foreign = Path.Combine(_projectDir, "BACKLOG", "invader", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user content");

        await CreateFeatureAsync(project.Id, "invader", "Invader");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await File.ReadAllTextAsync(foreign)).ShouldBe("user content");

        var report = await response.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        var skip = report!.Skipped.ShouldHaveSingleItem();
        skip.RelativePath.ShouldBe("BACKLOG/invader/feature.md");
        skip.Reason.ShouldContain("not tracked by Overmind");
        report.Files.ShouldNotContain(f => f.RelativePath == "BACKLOG/invader/feature.md");
        report.Files.ShouldContain(f => f.RelativePath == "BACKLOG.md");
    }

    [Fact]
    public async Task Materialize_EverythingGuarded_Returns409ListingSkipsInDetail()
    {
        var project = await CreateProjectWithMaterializationAsync("on");

        var foreign = Path.Combine(_projectDir, "BACKLOG", "invader", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user content");

        await CreateFeatureAsync(project.Id, "invader", "Invader");

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, (await File.ReadAllTextAsync(indexPath))
            .Replace("`[ ]`", "`[x]`", StringComparison.Ordinal));
        var foreignBytes = await File.ReadAllBytesAsync(foreign);
        var indexBytes = await File.ReadAllBytesAsync(indexPath);

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = false });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(409);
        problem!.Detail.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("nothing was written");
        problem.Detail.ShouldContain("BACKLOG/invader/feature.md");
        problem.Detail.ShouldContain("BACKLOG.md");
        problem.Detail.ShouldContain("planning_upsert_artifact");

        (await File.ReadAllBytesAsync(foreign)).ShouldBe(foreignBytes);
        (await File.ReadAllBytesAsync(indexPath)).ShouldBe(indexBytes);
    }

    [Fact]
    public async Task Materialize_WithForce_AdoptsForeignFileAndWarns()
    {
        var project = await CreateProjectWithMaterializationAsync("on");

        var foreign = Path.Combine(_projectDir, "BACKLOG", "invader", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user content");

        await CreateFeatureAsync(project.Id, "invader", "Invader");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Strategy.ShouldBe("on");
        report.Warnings.Length.ShouldBe(1);
        report.Files.ShouldContain(f => f.RelativePath == "BACKLOG/invader/feature.md");

        (await File.ReadAllTextAsync(foreign)).ShouldContain("feature: invader");

        var drift = await _client.GetFromJsonAsync<DriftItemJson[]>(
            $"/api/projects/{project.Id}/planning/drift", JsonOptions);
        drift.ShouldBeEmpty();
    }

    [Fact]
    public async Task Materialize_TrackedFileHandEdited_SkipsItAndReturnsReasonInReport()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "# Task One", Order = 1 });

        var taskFile = Path.Combine(_projectDir, "BACKLOG", "feat", "tasks", "01-task-one.md");
        File.Exists(taskFile).ShouldBeTrue();
        await File.AppendAllTextAsync(taskFile, "external edit\n");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        var skip = report!.Skipped.ShouldHaveSingleItem();
        skip.RelativePath.ShouldBe("BACKLOG/feat/tasks/01-task-one.md");
        skip.Reason.ShouldContain("local edits");
        report.Files.ShouldContain(f => f.RelativePath == "BACKLOG/feat/feature.md");
        (await File.ReadAllTextAsync(taskFile)).ShouldContain("external edit");
    }

    [Fact]
    public async Task Materialize_TrackedFileHandEdited_WithForce_RegeneratesFileAndWarns()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "# Task One", Order = 1 });

        var taskFile = Path.Combine(_projectDir, "BACKLOG", "feat", "tasks", "01-task-one.md");
        File.Exists(taskFile).ShouldBeTrue();
        await File.AppendAllTextAsync(taskFile, "external edit\n");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<MaterializationReportJson>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {taskFile}");

        (await File.ReadAllTextAsync(taskFile)).ShouldNotContain("external edit");
        var drift = await _client.GetFromJsonAsync<DriftItemJson[]>(
            $"/api/projects/{project.Id}/planning/drift", JsonOptions);
        drift.ShouldBeEmpty();
    }

    [Fact]
    public async Task Drift_ReturnsCamelCaseItems()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "# Task One", Order = 1 });

        var taskFile = Path.Combine(_projectDir, "BACKLOG", "feat", "tasks", "01-task-one.md");
        File.Exists(taskFile).ShouldBeTrue();
        await File.AppendAllTextAsync(taskFile, "external edit\n");

        var items = await _client.GetFromJsonAsync<DriftItemJson[]>(
            $"/api/projects/{project.Id}/planning/drift", JsonOptions);

        items.ShouldNotBeNull();
        var item = items!.ShouldHaveSingleItem();
        item.ProjectId.ShouldBe(project.Id);
        item.FeatureSlug.ShouldBe("feat");
        item.RelativePath.ShouldBe("BACKLOG/feat/tasks/01-task-one.md");
        item.Kind.ShouldBe("modified");
    }

    [Fact]
    public async Task DiskPathChange_Returns409WhilePlanningFilesTracked()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { Name = project.Name, DiskPath = Path.Combine(_sandboxDir, "elsewhere"), Description = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ArtifactWrite_InOnMode_MaterializesFile()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "myfeat", "My Feature");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/myfeat/artifacts",
            new { Kind = "spec", Body = "# Spec\n\n<scope>\nx\n</scope>" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var specFile = Path.Combine(_projectDir, "BACKLOG", "myfeat", "spec.md");
        File.Exists(specFile).ShouldBeTrue();
        (await File.ReadAllTextAsync(specFile)).ShouldContain("<scope>\nx\n</scope>");

        var featureFile = Path.Combine(_projectDir, "BACKLOG", "myfeat", "feature.md");
        (await File.ReadAllTextAsync(featureFile)).ShouldContain("# Feature myfeat — My Feature");

        var index = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md"));
        index.ShouldContain("- `[~]` myfeat → `BACKLOG/myfeat/` — My Feature");
    }

    private async Task<ProjectJson> CreateProjectWithMaterializationAsync(string strategy)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"mat-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();

        var workflow = await _client.PutAsJsonAsync(
            $"/api/projects/{project!.Id}/planning/workflow",
            new { Materialization = strategy });
        workflow.StatusCode.ShouldBe(HttpStatusCode.OK);

        return project;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = string.Empty });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record MaterializationReportJson(
        Guid ProjectId, string Strategy, MaterializedFileJson[] Files, string[] Warnings,
        MaterializationSkipJson[] Skipped);

    public sealed record MaterializedFileJson(string RelativePath, string Sha256);

    public sealed record MaterializationSkipJson(string RelativePath, string Reason);

    public sealed record DriftItemJson(Guid ProjectId, string? FeatureSlug, string RelativePath, string Kind);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
