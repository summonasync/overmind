using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class BacklogImportMaterializationTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;
    private readonly string _backlogDir;

    public BacklogImportMaterializationTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-import-mat-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        _dataDir = Path.Combine(_sandboxDir, "data");
        _backlogDir = Path.Combine(_projectDir, "BACKLOG");
        Directory.CreateDirectory(_backlogDir);
    }


    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await TestPlanningDispatcher.EnableSyncModeAsync(ctx);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Import_TracksSourceFiles_FirstMaterializeIsByteStableNoOpWithoutForce()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();

        using var ctx = _db.CreateContext();
        var report = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);

        report.FeaturesCreated.ShouldBe(1);
        report.ArtifactsCreated.ShouldBe(2);
        report.NextStep.ShouldBe("3 files tracked, materialize is a no-op");

        var pinnedTaskPath = Path.Combine(_backlogDir, "010-demo", "tasks", "01-first.md");
        File.Exists(pinnedTaskPath).ShouldBeTrue();
        File.Exists(Path.Combine(_backlogDir, "010-demo", "tasks", "01-task-one.md")).ShouldBeFalse();
        (await File.ReadAllTextAsync(pinnedTaskPath)).ShouldContain("# Task one");

        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        var rows = await ctx.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Select(m => m.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ShouldBe(
            new[]
            {
                "BACKLOG/010-demo/feature.md",
                "BACKLOG/010-demo/spec.md",
                "BACKLOG/010-demo/tasks/01-first.md",
            });
        rows.Single(m => m.RelativePath == "BACKLOG/010-demo/tasks/01-first.md").FeatureId.ShouldBe(feature.Id);

        var materializer = CreateMaterializer(ctx);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();

        var taskBytesBefore = await File.ReadAllBytesAsync(pinnedTaskPath);

        var second = await materializer.SyncAsync(projectId, force: false, CancellationToken.None);
        second.Warnings.ShouldBeEmpty();

        (await File.ReadAllBytesAsync(pinnedTaskPath)).ShouldBe(taskBytesBefore);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();

        var reimport = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        reimport.ArtifactsUpdated.ShouldBe(0);
        reimport.Skipped.ShouldNotContain(s => s.Contains("duplicate order", StringComparison.Ordinal));
        (await File.ReadAllBytesAsync(pinnedTaskPath)).ShouldBe(taskBytesBefore);
    }

    [Fact]
    public async Task Import_BackfillsFeatureIdOnLegacySeedRowAndPinsName()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();

        var bytes = await File.ReadAllBytesAsync(Path.Combine(_backlogDir, "010-demo", "tasks", "01-first.md"));
        using (var seedCtx = _db.CreateContext())
        {
            seedCtx.PlanningMaterializedFiles.Add(PlanningMaterializedFile.Create(
                projectId,
                featureId: null,
                "BACKLOG/010-demo/tasks/01-first.md",
                Convert.ToHexString(SHA256.HashData(bytes))));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = _db.CreateContext();
        var report = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);

        report.FeaturesCreated.ShouldBe(1);
        var sourcePath = Path.Combine(_backlogDir, "010-demo", "tasks", "01-first.md");
        File.Exists(sourcePath).ShouldBeTrue();
        File.Exists(Path.Combine(_backlogDir, "010-demo", "tasks", "01-task-one.md")).ShouldBeFalse();

        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        var row = await ctx.PlanningMaterializedFiles.SingleAsync(m =>
            m.ProjectId == projectId && m.RelativePath == "BACKLOG/010-demo/tasks/01-first.md");
        row.FeatureId.ShouldBe(feature.Id);
        (await File.ReadAllTextAsync(sourcePath)).ShouldContain("task: 01");
        (await CreateMaterializer(ctx).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Seed_SameOrderPlainTasksInBatch_TracksFirstAlphabeticalOnly()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteRaw("030-dup/tasks/01-a.md", "# Alpha\n");
        WriteRaw("030-dup/tasks/01-b.md", "# Beta\n");

        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "030-dup", "Dup", "body");
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();

        var seeder = new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)));
        var tasksDir = Path.Combine(_backlogDir, "030-dup", "tasks");
        var alpha = Path.Combine(tasksDir, "01-a.md");
        var beta = Path.Combine(tasksDir, "01-b.md");
        var result = await seeder.SeedAsync(
            projectId,
            feature.Id,
            new[] { new BacklogImportSourceFile(alpha, await ShaOfAsync(alpha)), new BacklogImportSourceFile(beta, await ShaOfAsync(beta)) },
            CancellationToken.None);
        await ctx.SaveChangesAsync();

        result.Tracked.ShouldBe(1);
        var rows = await ctx.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.RelativePath)
            .ToListAsync();
        rows.ShouldBe(new[] { "BACKLOG/030-dup/tasks/01-a.md" });
    }

    [Fact]
    public async Task Seed_ReSeedWithChangedContent_RefreshesShaWithoutDuplicates()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteRaw("040-upsert/feature.md", "# Original\n");

        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "040-upsert", "Upsert", "body");
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();

        var path = Path.Combine(_backlogDir, "040-upsert", "feature.md");
        var seeder = new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)));

        var first = await seeder.SeedAsync(
            projectId, feature.Id, [new BacklogImportSourceFile(path, await ShaOfAsync(path))],
            CancellationToken.None);
        await ctx.SaveChangesAsync();
        first.Tracked.ShouldBe(1);

        await File.WriteAllTextAsync(path, "# Changed\n");
        var second = await seeder.SeedAsync(
            projectId, feature.Id, [new BacklogImportSourceFile(path, await ShaOfAsync(path))],
            CancellationToken.None);
        await ctx.SaveChangesAsync();
        second.Tracked.ShouldBe(0);

        var rows = await ctx.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync();
        rows.Count.ShouldBe(1);
        rows.Single().Sha256.ShouldBe(await ShaOfAsync(path));
    }

    [Fact]
    public async Task Import_ReviewFeatureStatus_RoundTripsThroughMaterializedFile()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteRaw("011-review/feature.md",
            """
            ---
            feature: 011
            slug: review
            status: review
            version: 1
            created: 2026-09-01
            ---

            # Feature 011 — Review

            Body under review.
            """);

        using var ctx = _db.CreateContext();
        var first = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        first.FeaturesCreated.ShouldBe(1);
        first.Skipped.ShouldBeEmpty();

        var materialized = await File.ReadAllTextAsync(Path.Combine(_backlogDir, "011-review", "feature.md"));
        materialized.ShouldContain("status: review");

        var second = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        second.Skipped.ShouldBeEmpty();

        var feature = await ctx.PlanningFeatures.SingleAsync(f => f.ProjectId == projectId && f.Slug == "011-review");
        feature.Status.ShouldBe(PlanningFeatureStatus.Review);
    }

    [Fact]
    public async Task Import_PreservedFrontmatterKeys_RoundTripThroughMaterializedFile()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();
        WriteRaw("024-keys/feature.md",
            """
            ---
            feature: 024
            slug: keys
            status: draft
            version: 1
            created: 2026-09-01
            ---

            # Feature 024 — Keys
            """);
        WriteRaw("024-keys/tasks/01-key-task.md",
            """
            ---
            feature: 024
            task: 01
            slug: keys
            status: todo
            review: light
            created: 2026-09-01
            epic: demo
            risk_level: high
            ---

            # Key task
            """);

        using var ctx = _db.CreateContext();
        var first = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        first.ArtifactsCreated.ShouldBe(3);

        var materialized = await File.ReadAllTextAsync(
            Path.Combine(_backlogDir, "024-keys", "tasks", "01-key-task.md"));
        materialized.ShouldContain(
            "created: 2026-09-01\n" +
            "epic: demo\n" +
            "risk_level: high\n" +
            "---\n\n" +
            "# Key task");

        var second = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        second.ArtifactsUpdated.ShouldBe(0);
        second.NewKeys.ShouldBeEmpty();
        second.NextStep.ShouldBeNull();

        var feature = await ctx.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstAsync(f => f.ProjectId == projectId && f.Slug == "024-keys");
        feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Task).PreservedFrontmatter
            .ShouldBe("{\"epic\":\"demo\",\"risk_level\":\"high\"}");
    }

    [Fact]
    public async Task Import_MaterializationOff_DoesNotTrackFilesAndReportsNextStep()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Off);
        WriteDemoRepo();

        using var ctx = _db.CreateContext();
        var report = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);

        report.FeaturesCreated.ShouldBe(1);
        report.NextStep.ShouldBe(
            "materialization is 'off'; set planning materialization to 'on' to write imported features to the repo");
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(0);
        (await File.ReadAllTextAsync(Path.Combine(_backlogDir, "010-demo", "feature.md")))
            .ShouldContain("status: ready");
    }

    [Fact]
    public async Task Import_OnProject_RowShasMatchFileBytesOnDisk()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();

        using var ctx = _db.CreateContext();
        var report = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);

        report.FeaturesCreated.ShouldBe(1);
        var rows = await ctx.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Select(m => m.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ShouldBe(new[]
        {
            "BACKLOG/010-demo/feature.md",
            "BACKLOG/010-demo/spec.md",
            "BACKLOG/010-demo/tasks/01-first.md",
        });

        foreach (var row in rows)
        {
            row.Sha256.ShouldBe(await ShaOfAsync(Path.Combine(_projectDir, row.RelativePath)));
        }

        (await CreateMaterializer(ctx).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_ReImportAfterDiskEdit_RefreshesRowShaAndKeepsSyncClean()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();

        using var ctx = _db.CreateContext();
        var first = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        first.FeaturesCreated.ShouldBe(1);

        var taskPath = Path.Combine(_backlogDir, "010-demo", "tasks", "01-first.md");
        await File.WriteAllTextAsync(taskPath, await File.ReadAllTextAsync(taskPath) + "\nLate requirement.\n");

        var second = await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);
        second.ArtifactsUpdated.ShouldBe(1);

        var row = await ctx.PlanningMaterializedFiles.SingleAsync(m =>
            m.ProjectId == projectId && m.RelativePath == "BACKLOG/010-demo/tasks/01-first.md");
        row.Sha256.ShouldBe(await ShaOfAsync(taskPath));
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(3);

        var materializer = CreateMaterializer(ctx);
        var sync = await materializer.SyncAsync(projectId, force: false, CancellationToken.None);
        sync.Warnings.ShouldBeEmpty();
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_ThenBoardMutation_MaterializesWithoutGuardRefusal()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        WriteDemoRepo();

        using var ctx = _db.CreateContext();
        await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_projectDir, projectId), CancellationToken.None);

        var features = new PlanningFeatureService(
            ctx,
            CreateMaterializer(ctx),
            new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx))),
            new TaskDependencyService(ctx, TestPlanningDispatcher.Create(ctx, CreateMaterializer(ctx))),
            TestPlanningDispatcher.Create(ctx, CreateMaterializer(ctx)),
            NullLogger<PlanningFeatureService>.Instance);
        await features.UpdateAsync(projectId, "010-demo", title: "Demo feature retitled", body: null, status: null);
        await features.CreateAsync(projectId, "020-second", "Second feature", "Fresh body");

        (await File.ReadAllTextAsync(Path.Combine(_backlogDir, "010-demo", "feature.md")))
            .ShouldContain("Demo feature retitled");
        File.Exists(Path.Combine(_backlogDir, "020-second", "feature.md")).ShouldBeTrue();

        var project = await ctx.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializationError.ShouldBeNull();
        project.LastMaterializedAt.ShouldNotBeNull();

        (await CreateMaterializer(ctx).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    private async Task<Guid> SeedProjectAsync(PlanningMaterialization strategy)
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(strategy, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private BacklogImporter CreateImporter(OvermindDbContext ctx)
    {
        var materializer = CreateMaterializer(ctx);
        var seeder = new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)));
        var features = new PlanningFeatureService(
            ctx,
            materializer,
            seeder,
            new TaskDependencyService(ctx, TestPlanningDispatcher.Create(ctx, materializer)),
            TestPlanningDispatcher.Create(ctx, materializer),
            NullLogger<PlanningFeatureService>.Instance);
        var keys = new FrontmatterKeyRegistry(ctx, NullLogger<FrontmatterKeyRegistry>.Instance);
        return new BacklogImporter(features, keys);
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private void WriteDemoRepo()
    {
        WriteRaw("010-demo/feature.md",
            """
            ---
            feature: 010
            slug: demo
            status: ready
            version: 1
            created: 2026-09-01
            ---

            # Feature 010 — Demo feature title

            <goal>
            Build the demo.
            </goal>
            """);
        WriteRaw("010-demo/spec.md",
            """
            ---
            feature: 010
            slug: demo
            status: draft
            created: 2026-09-01
            ---

            # Spec 010 — Demo

            Spec body.
            """);
        WriteRaw("010-demo/tasks/01-first.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Task one

            <goal>
            First task.
            </goal>
            """);
    }

    private void WriteRaw(string relativePath, string content)
    {
        var path = Path.Combine(_backlogDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task<string> ShaOfAsync(string path) =>
        Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
}
