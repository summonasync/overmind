using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningImportApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-import-scan-{Guid.NewGuid():N}");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningImportApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_FindsBacklogFoldersWithCounts()
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(ScanUrl(project.Id), new { SubPath = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ScanJson>(JsonOptions);
        report!.Root.ShouldEndWith("BACKLOG");
        report.Entries.Length.ShouldBe(2);

        var demo = report.Entries[0];
        demo.Folder.ShouldBe("058-demo");
        demo.Slug.ShouldBe("058-demo");
        demo.Title.ShouldBe("Demo import");
        demo.Status.ShouldBe("inProgress");
        demo.Specs.ShouldBe(1);
        demo.Plans.ShouldBe(0);
        demo.Results.ShouldBe(0);
        demo.Tasks.ShouldBe(1);
        demo.AlreadyImported.ShouldBeFalse();
        demo.Error.ShouldBeNull();

        var other = report.Entries[1];
        other.Folder.ShouldBe("060-other");
        other.Slug.ShouldBe("060-other");
        other.Status.ShouldBe("ready");
        other.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Scan_MarksAlreadyImportedFoldersBySlug()
    {
        var project = await CreateProjectWithBacklogAsync();
        var import = await _client.PostAsJsonAsync(
            ImportUrl(project.Id), new { Folder = "058-demo" });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync(ScanUrl(project.Id), new { SubPath = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ScanJson>(JsonOptions);
        report!.Entries.Single(e => e.Folder == "058-demo").AlreadyImported.ShouldBeTrue();
        report.Entries.Single(e => e.Folder == "060-other").AlreadyImported.ShouldBeFalse();
    }

    [Fact]
    public async Task Scan_BrokenFolderIsIsolatedWithErrorNote()
    {
        var project = await CreateProjectWithBacklogAsync();
        Directory.CreateDirectory(Path.Combine(_root, "BACKLOG", "061-broken"));

        var response = await _client.PostAsJsonAsync(ScanUrl(project.Id), new { SubPath = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ScanJson>(JsonOptions);
        report!.Entries.Length.ShouldBe(3);

        var broken = report.Entries.Single(e => e.Folder == "061-broken");
        broken.Slug.ShouldBeNull();
        broken.Title.ShouldBeNull();
        broken.Status.ShouldBeNull();
        broken.AlreadyImported.ShouldBeFalse();
        broken.Error.ShouldNotBeNull();
        broken.Error.ShouldContain("feature.md");

        report.Entries.Single(e => e.Folder == "058-demo").Slug.ShouldBe("058-demo");
        report.Entries.Single(e => e.Folder == "060-other").Slug.ShouldBe("060-other");
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData("/etc")]
    public async Task Scan_SubPathOutsideProjectRoot_Returns400(string subPath)
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(ScanUrl(project.Id), new { SubPath = subPath });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain("escapes the project directory");
    }

    [Fact]
    public async Task Scan_FeatureFolderSubPath_ReturnsSingleEntry()
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(
            ScanUrl(project.Id), new { SubPath = "BACKLOG/058-demo" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ScanJson>(JsonOptions);
        report!.Root.ShouldEndWith("058-demo");
        report.Entries.Length.ShouldBe(1);
        report.Entries[0].Folder.ShouldBe("058-demo");
        report.Entries[0].Specs.ShouldBe(1);
        report.Entries[0].Tasks.ShouldBe(1);
    }

    [Fact]
    public async Task Scan_DoesNotTouchTheBoard()
    {
        var project = await CreateProjectWithBacklogAsync();

        var before = await (await _client.GetAsync(BoardUrl(project.Id))).Content
            .ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        before!.Length.ShouldBe(0);

        var scan = await _client.PostAsJsonAsync(ScanUrl(project.Id), new { SubPath = (string?)null });
        scan.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await (await _client.GetAsync(BoardUrl(project.Id))).Content
            .ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        after!.Length.ShouldBe(0);

        var import = await _client.PostAsJsonAsync(
            ImportUrl(project.Id), new { Folder = "058-demo" });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await import.Content.ReadFromJsonAsync<FeatureImportJson>(JsonOptions);
        report!.Created.ShouldBeTrue();
    }

    [Fact]
    public async Task Import_FolderName_ImportsWithReportShape()
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(
            ImportUrl(project.Id), new { Folder = "058-demo" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<FeatureImportJson>(JsonOptions);
        report!.Slug.ShouldBe("058-demo");
        report.Title.ShouldBe("Demo import");
        report.Status.ShouldBe("inProgress");
        report.Created.ShouldBeTrue();
        report.ArtifactsCreated.ShouldBe(2);
        report.ArtifactsUpdated.ShouldBe(0);
        report.TrackedFiles.ShouldBe(3);
        report.Materialization.ShouldBe("on");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/058-demo", JsonOptions);
        feature!.Status.ShouldBe("inProgress");
        var spec = feature.Artifacts.Single(a => a.Kind == "spec");
        spec.Status.ShouldBe("validated");
        var task = feature.Artifacts.Single(a => a.Kind == "task");
        task.Status.ShouldBe("review");
        task.Review.ShouldBe("hard");
        task.Brief.ShouldBe("Wire the import endpoint");
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("BACKLOG/058-demo")]
    public async Task Import_NonFolderName_ReturnsValidationProblem(string folder)
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(ImportUrl(project.Id), new { Folder = folder });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var messages = body.GetProperty("errors").EnumerateObject().SelectMany(e => e.Value.EnumerateArray());
        messages.ShouldContain(m => m.GetString()!.Contains("single BACKLOG folder name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_UnknownFolder_Returns400()
    {
        var project = await CreateProjectWithBacklogAsync();

        var response = await _client.PostAsJsonAsync(
            ImportUrl(project.Id), new { Folder = "999-missing" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ImportFailed");
        problem.Detail!.ShouldContain("is not a directory");
    }

    [Fact]
    public async Task Import_UnknownProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            ImportUrl(Guid.NewGuid()), new { Folder = "058-demo" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Import_StatusRestoredToDone_RetiresTheLaneThroughTheEndpoint()
    {
        var backlog = Path.Combine(_root, "BACKLOG");
        var closed = Path.Combine(backlog, "061-closed");
        Directory.CreateDirectory(closed);
        await File.WriteAllTextAsync(Path.Combine(closed, "feature.md"),
            """
            ---
            feature: 061
            slug: closed
            status: done
            priority: p2
            version: 1
            created: 2026-09-23
            ---

            # Feature 061 — Closed on disk

            <goal>
            Land as done.
            </goal>
            """);

        var project = await CreateProjectWithBacklogAsync();
        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features",
            new { Slug = "061-closed", Title = "Closed on disk", Body = "" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bound = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/061-closed",
            new { Lane = "frontend" });
        bound.StatusCode.ShouldBe(HttpStatusCode.OK);

        var import = await _client.PostAsJsonAsync(ImportUrl(project.Id), new { Folder = "061-closed" });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);

        var roster = await _client.GetFromJsonAsync<RosterJson>(
            $"/api/projects/{project.Id}/planning/lanes", JsonOptions);
        roster!.Lanes.ShouldBeEmpty();
    }

    private static string ScanUrl(Guid projectId) =>
        $"/api/projects/{projectId}/planning/import/scan";

    private static string ImportUrl(Guid projectId) =>
        $"/api/projects/{projectId}/planning/import";

    private static string BoardUrl(Guid projectId) =>
        $"/api/projects/{projectId}/planning/features";

    private async Task<ProjectJson> CreateProjectWithBacklogAsync()
    {
        var backlog = Path.Combine(_root, "BACKLOG");
        var demo = Path.Combine(backlog, "058-demo");
        Directory.CreateDirectory(Path.Combine(demo, "tasks"));
        await File.WriteAllTextAsync(Path.Combine(demo, "feature.md"),
            """
            ---
            feature: 058
            slug: demo
            status: in_progress
            priority: p1
            version: 2
            created: 2026-09-20
            ---

            # Feature 058 — Demo import

            <goal>
            Import it whole.
            </goal>
            """);
        await File.WriteAllTextAsync(Path.Combine(demo, "spec.md"),
            """
            ---
            feature: 058
            slug: demo
            status: validated
            created: 2026-09-20
            validated: 2026-09-21
            ---

            # Spec 058 — Demo import

            Spec body.
            """);
        await File.WriteAllTextAsync(Path.Combine(demo, "tasks", "01-wire.md"),
            """
            ---
            feature: 058
            task: 01
            slug: demo
            status: review
            review: hard
            created: 2026-09-20
            brief: Wire the import endpoint
            ---

            # Task 01 — Wire

            Wire it.
            """);

        var other = Path.Combine(backlog, "060-other");
        Directory.CreateDirectory(other);
        await File.WriteAllTextAsync(Path.Combine(other, "feature.md"),
            """
            ---
            feature: 060
            slug: other
            status: ready
            priority: p3
            version: 1
            created: 2026-09-22
            ---

            # Feature 060 — Other import

            Just a feature folder.
            """);

        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"import-scan-{Guid.NewGuid():N}",
            DiskPath = _root,
            Description = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record ScanJson(string Root, ScanEntryJson[] Entries);

    public sealed record ScanEntryJson(
        string Folder, string? Slug, string? Title, string? Status,
        int Specs, int Plans, int Results, int Tasks,
        bool AlreadyImported, string? Error);

    public sealed record FeatureImportJson(
        string Slug, string Title, string Status, bool Created,
        int ArtifactsCreated, int ArtifactsUpdated, string[] Notes,
        int TrackedFiles, string Materialization, string? NextStep);

    public sealed record FeatureJson(
        string Slug, string Title, string Body, string Status,
        int Version, DateTime CreatedAt, DateTime UpdatedAt, ArtifactJson[] Artifacts);

    public sealed record ArtifactJson(
        Guid Id, string Kind, string Status, string Review,
        int Order, string Body, DateTime? FrozenAt, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, LogLineJson[] Log, string? Brief = null);

    public sealed record LogLineJson(string? Date, string Text);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);

    public sealed record RosterJson(LaneRowJson[] Lanes);

    public sealed record LaneRowJson(string Name);
}
