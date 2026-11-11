using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningMaterializationTests : IDisposable, IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        var put = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationSync = "on" });
        put.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
    public async Task Materialize_WithoutForce_RefusesForeignFileAndWritesNothing()
    {
        var project = await CreateProjectWithMaterializationAsync("on");

        var foreign = Path.Combine(_projectDir, "BACKLOG", "invader", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user content");
        var foreignBytes = await File.ReadAllBytesAsync(foreign);

        await CreateFeatureAsync(project.Id, "invader", "Invader");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = false });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(409);
        problem!.Detail.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("BACKLOG/invader/feature.md");
        problem.Detail.ShouldContain("not tracked by Overmind");

        (await File.ReadAllBytesAsync(foreign)).ShouldBe(foreignBytes);
        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
    }

    [Fact]
    public async Task Materialize_EverythingGuarded_Returns409ListingRefusalsInDetail()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "invader", "Invader");

        var featureDoc = Path.Combine(_projectDir, "BACKLOG", "invader", "feature.md");
        File.Exists(featureDoc).ShouldBeTrue();
        await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

        var foreign = Path.Combine(_projectDir, "BACKLOG", "second", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user content");
        var foreignBytes = await File.ReadAllBytesAsync(foreign);

        await CreateFeatureAsync(project.Id, "second", "Second");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/materialize", new { Force = false });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(409);
        problem!.Detail.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("nothing was written");
        problem.Detail.ShouldContain("BACKLOG/invader/feature.md");
        problem.Detail.ShouldContain("BACKLOG/second/feature.md");
        problem.Detail.ShouldContain("planning_upsert_artifact");

        (await File.ReadAllBytesAsync(foreign)).ShouldBe(foreignBytes);
        (await File.ReadAllTextAsync(featureDoc)).ShouldContain("hand edit");
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
        report.LastSuccess.ShouldNotBeNull();
        report.LastAttempt.ShouldNotBeNull();

        (await File.ReadAllTextAsync(foreign)).ShouldContain("feature: invader");

        var drift = await _client.GetFromJsonAsync<DriftItemJson[]>(
            $"/api/projects/{project.Id}/planning/drift", JsonOptions);
        drift.ShouldBeEmpty();
    }

    [Fact]
    public async Task Materialize_TrackedFileHandEdited_RefusesAndReturnsReasonInDetail()
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

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe(409);
        problem!.Detail.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("BACKLOG/feat/tasks/01-task-one.md");
        problem.Detail.ShouldContain("local edits");
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
        report.LastSuccess.ShouldNotBeNull();
        report.LastAttempt.ShouldNotBeNull();

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
    public async Task DiskPathChange_FullMirrorCopyAtCandidate_SucceedsAndUpdatesPath()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var candidate = Path.Combine(_sandboxDir, "moved");
        CopyDirectory(_projectDir, candidate);

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { Name = project.Name, DiskPath = candidate, Description = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.DiskPath.ShouldBe(candidate);
    }

    [Fact]
    public async Task DiskPathChange_DriftedAndMissingMirrorFiles_Returns409WithNamesAndTotals()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "# Task One", Order = 1 });

        var candidate = Path.Combine(_sandboxDir, "moved");
        CopyDirectory(_projectDir, candidate);
        await File.AppendAllTextAsync(
            Path.Combine(candidate, "BACKLOG", "feat", "feature.md"), "\nhand edit\n");
        File.Delete(Path.Combine(candidate, "BACKLOG", "feat", "tasks", "01-task-one.md"));

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { Name = project.Name, DiskPath = candidate, Description = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Detail.ShouldNotBeNull();
        problem.Detail!.ShouldStartWith(
            "Mirror verification failed: 2 of 2 files drifted or missing at the candidate path.");
        problem.Detail.ShouldContain("BACKLOG/feat/feature.md");
        problem.Detail.ShouldContain("BACKLOG/feat/tasks/01-task-one.md");
    }

    [Fact]
    public async Task DiskPathChange_TrackedMirrorWithoutCandidateDirectory_Returns400BeforeVerification()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { Name = project.Name, DiskPath = Path.Combine(_sandboxDir, "elsewhere"), Description = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Detail!.ShouldContain("Candidate path does not exist or is not a directory.");
    }

    [Fact]
    public async Task DiskPathChange_EmptyMirror_StillChangesFreelyWithoutCandidate()
    {
        var project = await CreateProjectWithMaterializationAsync("off");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}",
            new { Name = project.Name, DiskPath = Path.Combine(_sandboxDir, "never-created"), Description = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        updated.ShouldNotBeNull();
        updated!.DiskPath.ShouldBe(Path.Combine(_sandboxDir, "never-created"));
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
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
    }

    [Fact]
    public async Task MaterializationStatus_AfterCleanPass_ReturnsCamelCaseStructuredMirrorState()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.GetAsync($"/api/agent/projects/{project.Id}/planning/materialization-status");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("\"lastSuccess\":");
        raw.ShouldContain("\"lastAttempt\":");
        raw.ShouldContain("\"lastAttemptReason\":");
        raw.ShouldContain("\"trackedFiles\":1");
        raw.ShouldContain("\"stalePaths\":[]");
        raw.ShouldNotContain("LastSuccess", Case.Sensitive);

        var status = await response.Content.ReadFromJsonAsync<MaterializationStatusJson>(JsonOptions);
        status.ShouldNotBeNull();
        status!.ProjectId.ShouldBe(project.Id);
        status.LastSuccess.ShouldNotBeNull();
        status.LastAttempt.ShouldNotBeNull();
        status.LastAttemptReason.ShouldBeNull();
        status.Strategy.ShouldBe("on");
        status.TargetDir.ShouldBe(Path.Combine(_projectDir, "BACKLOG"));
        status.TrackedFiles.ShouldBe(1);
        status.StalePaths.ShouldBeEmpty();

        var run = status.LastRun.ShouldNotBeNull();
        run!.Atomic.ShouldBeTrue();
        run.Written.ShouldContain("BACKLOG/feat/feature.md");
        run.Skipped.ShouldBeEmpty();
        run.Refused.ShouldBeEmpty();
    }

    [Fact]
    public async Task MaterializationStatus_DriftedTrackedFile_AppearsInStalePaths()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        var featureDoc = Path.Combine(_projectDir, "BACKLOG", "feat", "feature.md");
        await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

        var status = await _client.GetFromJsonAsync<MaterializationStatusJson>(
            $"/api/agent/projects/{project.Id}/planning/materialization-status", JsonOptions);

        status.ShouldNotBeNull();
        status!.StalePaths.ShouldHaveSingleItem().ShouldBe("BACKLOG/feat/feature.md");
        status.LastRun.ShouldNotBeNull();
    }

    [Fact]
    public async Task MaterializationStatus_RefusedRun_TreatsEveryTrackedFileAsUnhandled()
    {
        var project = await CreateProjectWithMaterializationAsync("on");
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        var featureDoc = Path.Combine(_projectDir, "BACKLOG", "feat", "feature.md");
        await File.AppendAllTextAsync(featureDoc, "\nhand edit\n");

        await CreateFeatureAsync(project.Id, "second", "Second");

        var status = await _client.GetFromJsonAsync<MaterializationStatusJson>(
            $"/api/agent/projects/{project.Id}/planning/materialization-status", JsonOptions);

        status.ShouldNotBeNull();
        status!.LastAttemptReason.ShouldNotBeNull();
        status.LastAttemptReason!.ShouldContain("local edits");
        var run = status.LastRun.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        run.Refused.ShouldNotBeEmpty();
        status.StalePaths.Length.ShouldBe(status.TrackedFiles);
        status.StalePaths.ShouldContain("BACKLOG/feat/feature.md");
    }

    [Fact]
    public async Task MaterializationStatus_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/agent/projects/{Guid.NewGuid()}/planning/materialization-status");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        MaterializationSkipJson[] Skipped, DateTime? LastSuccess, DateTime? LastAttempt);

    public sealed record MaterializedFileJson(string RelativePath, string Sha256);

    public sealed record MaterializationSkipJson(string RelativePath, string Reason);

    public sealed record DriftItemJson(Guid ProjectId, string? FeatureSlug, string RelativePath, string Kind);

    public sealed record MaterializationStatusJson(
        Guid ProjectId, DateTime? LastSuccess, DateTime? LastAttempt, string? LastAttemptReason,
        MaterializationRunJson? LastRun, int TrackedFiles, string[] StalePaths, string Strategy, string TargetDir);

    public sealed record MaterializationRunJson(
        DateTime StartedAt, DateTime FinishedAt, bool Atomic, string[] Written,
        MaterializationSkipJson[] Skipped, MaterializationSkipJson[] Refused);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
