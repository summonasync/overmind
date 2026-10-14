using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using System.Security.Cryptography;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializerTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningMaterializerTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-mat-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        _dataDir = Path.Combine(_sandboxDir, "data");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task On_WritesFullLayoutWithNamingAndFrontmatter()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);
        await SeedFeatureAsync(projectId, "auth", "Auth", "body", PlanningFeatureStatus.Done);

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var featureDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md"));
        featureDoc.ShouldBe(
            "---\n" +
            "feature: 006\n" +
            "slug: demo\n" +
            "status: ready\n" +
            "priority: p2\n" +
            "version: 1\n" +
            $"created: {DateTime.UtcNow:yyyy-MM-dd}\n" +
            "---\n\n" +
            "# Feature 006 — Demo Feature\n\n" +
            "<goal>\nShip it.\n</goal>");

        var specDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md"));
        specDoc.ShouldContain("status: draft\n");
        specDoc.ShouldNotContain("validated:");
        specDoc.ShouldEndWith("# Spec 006 — Demo Feature\n\n<scope>\nSpec body.\n</scope>");

        var taskOne = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md"));
        taskOne.ShouldContain("task: 01\n");
        taskOne.ShouldContain("slug: demo\n");
        taskOne.ShouldContain("review: hard\n");
        taskOne.ShouldContain("status: todo\n");
        taskOne.ShouldEndWith("# First Task\n\n<goal>\nDo it.\n</goal>");

        var taskTwoPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "02-task-02.md");
        File.Exists(taskTwoPath).ShouldBeTrue();

        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "plan.md")).ShouldBeFalse();

        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();

        var rows = await ctx.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Select(r => r.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ShouldBe(
            new[]
            {
                "BACKLOG/006-demo/feature.md",
                "BACKLOG/006-demo/spec.md",
                "BACKLOG/006-demo/tasks/01-first-task.md",
                "BACKLOG/006-demo/tasks/02-task-02.md",
                "BACKLOG/auth/feature.md",
            });

        report.Files.Count.ShouldBe(5);
        report.Warnings.ShouldBeEmpty();
        (await CreateMaterializer(ctx).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task On_HeadinglessTaskWithBrief_FilenameCarriesBriefSlug()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using (var ctx = _db.CreateContext())
        {
            var feature = PlanningFeature.Create(projectId, "076-demo", "Naming", "<goal>\nShip it.\n</goal>");
            ctx.PlanningFeatures.Add(feature);

            var latin = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "<goal>\nDo it.\n</goal>", 1);
            latin.SetBrief("Data + EF: blockedBy on PlanningFeature");
            var cyrillic = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "<goal>\nТоже.\n</goal>", 2);
            cyrillic.SetBrief("CPM: обновление пакетов Aspire 13.5");

            ctx.PlanningArtifacts.AddRange(latin, cyrillic);
            await ctx.SaveChangesAsync();
        }

        using var sync = _db.CreateContext();
        await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);

        var tasksDir = Path.Combine(_projectDir, "BACKLOG", "076-demo", "tasks");
        File.Exists(Path.Combine(tasksDir, "01-data-ef-blockedby-on-planningfeature.md")).ShouldBeTrue();
        File.Exists(Path.Combine(tasksDir, "02-cpm-obnovlenie-paketov-aspire-13-5.md")).ShouldBeTrue();
        File.Exists(Path.Combine(tasksDir, "02-task-02.md")).ShouldBeFalse();
        (await CreateMaterializer(sync).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task On_BlockedFeature_WritesSortedDependsOnLine()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);
        await SeedFeatureAsync(projectId, "004-auth", "Auth", "body", PlanningFeatureStatus.Done);
        await SeedFeatureAsync(projectId, "008-storage", "Storage", "body", PlanningFeatureStatus.Done);

        using (var ctx = _db.CreateContext())
        {
            var features = await ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .Where(f => f.ProjectId == projectId)
                .ToListAsync();
            var demo = features.Single(f => f.Slug == "006-demo");
            demo.AddBlockedBy(features.Single(f => f.Slug == "008-storage"));
            demo.AddBlockedBy(features.Single(f => f.Slug == "004-auth"));
            await ctx.SaveChangesAsync();
        }

        using var syncCtx = _db.CreateContext();
        await CreateMaterializer(syncCtx).SyncAsync(projectId, ct: CancellationToken.None);

        var featureDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md"));
        featureDoc.ShouldContain(
            $"created: {DateTime.UtcNow:yyyy-MM-dd}\ndepends_on: 004-auth, 008-storage\n---");
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "004-auth", "feature.md")))
            .ShouldNotContain("depends_on");
    }

    [Fact]
    public async Task On_ReviewFeature_EmitsReviewStatus()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedFeatureAsync(projectId, "audit", "Audit", "body", PlanningFeatureStatus.Review);

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var featureDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "audit", "feature.md"));
        featureDoc.ShouldContain("status: review\n");
        featureDoc.ShouldNotContain("status: inProgress");
        report.Files.ShouldHaveSingleItem().RelativePath.ShouldBe("BACKLOG/audit/feature.md");
    }

    [Fact]
    public async Task Sync_LegacyIndexRow_StripsBlockKeepsProseByteIdenticalAndNeverTouchesTheFileAgain()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string prose = "# My BACKLOG\n\nHand-written intro.\n## Deferred\n- later\n";
        await File.WriteAllTextAsync(indexPath, LegacyIndexFile(prose, LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(prose);
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);

        var stripped = await File.ReadAllBytesAsync(indexPath);
        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "006-demo");
        feature.Update(null, "<goal>\nShip it v2.\n</goal>");
        await ctx.SaveChangesAsync();

        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllBytesAsync(indexPath)).ShouldBe(stripped);
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")))
            .ShouldContain("Ship it v2.");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_LegacyIndexRowWithoutMarkers_DropsRowAndKeepsHandFileUntouched()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string handContent = "# BACKLOG\n\n- [ ] human item\n";
        await File.WriteAllTextAsync(indexPath, handContent);
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(handContent);
        (await ctx.PlanningMaterializedFiles.CountAsync(m =>
                m.ProjectId == projectId && m.RelativePath == "BACKLOG.md"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Sync_FreshProject_WritesNoIndexAndNoRow()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
        report.Files.ShouldNotContain(f => f.RelativePath == "BACKLOG.md");
        (await ctx.PlanningMaterializedFiles.CountAsync(m =>
                m.ProjectId == projectId && m.RelativePath == "BACKLOG.md"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Drift_LegacyIndexRowIsScopedToBlockOnly()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, LegacyIndexFile("# BACKLOG\n\n", LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();

        await File.AppendAllTextAsync(indexPath, "\n## Human notes\n\ndo not touch\n");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();

        var edited = (await File.ReadAllTextAsync(indexPath)).Replace("`[ ]`", "`[x]`", StringComparison.Ordinal);
        await File.WriteAllTextAsync(indexPath, edited);

        var drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        var item = drift.ShouldHaveSingleItem();
        item.Kind.ShouldBe(DriftKind.Modified);
        item.RelativePath.ShouldBe("BACKLOG.md");
        item.FeatureSlug.ShouldBeNull();

        File.Delete(indexPath);

        drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        drift.ShouldHaveSingleItem().Kind.ShouldBe(DriftKind.Missing);
    }

    [Fact]
    public async Task Sync_UntrackedFileAtPlannedPath_RefusesAndWritesNothing()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var foreign = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user data");
        var before = await SnapshotTreeAsync(_projectDir);

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.OffendingPath.ShouldBe("BACKLOG/006-demo/feature.md");
        ex.Message.ShouldContain("BACKLOG/006-demo/feature.md");
        ex.Message.ShouldContain("not tracked by Overmind");

        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializedAt.ShouldBeNull();
        project.LastMaterializationAttemptAt.ShouldNotBeNull();
        project.LastMaterializationError.ShouldNotBeNull();
        project.LastMaterializationError!.ShouldContain("not tracked by Overmind");
        project.LastMaterializationFailedAt.ShouldNotBeNull();
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(0);

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        run.Skipped.ShouldBeEmpty();
        var refusal = run.Refused.ShouldHaveSingleItem();
        refusal.RelativePath.ShouldBe("BACKLOG/006-demo/feature.md");
        refusal.Reason.ShouldContain("not tracked by Overmind");
    }

    [Fact]
    public async Task Sync_Force_AdoptsUntrackedFileAndWarns()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var foreign = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user data");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        report.Warnings.ShouldHaveSingleItem().ShouldContain(foreign);
        (await File.ReadAllTextAsync(foreign)).ShouldContain("feature: 006");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_TrackedFilesHandEdited_RefusesWholeRunAndKeepsBytes()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        var specPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md");
        await File.AppendAllTextAsync(taskPath, "\nhand note\n");
        await File.WriteAllTextAsync(specPath, (await File.ReadAllTextAsync(specPath)) + "\nhand edit\n");
        var before = await SnapshotTreeAsync(_projectDir);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.OffendingPath.ShouldBeNull();
        ex.Message.ShouldContain("BACKLOG/006-demo/tasks/01-first-task.md");
        ex.Message.ShouldContain("BACKLOG/006-demo/spec.md");

        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        var firstSuccess = project.LastMaterializedAt!.Value;
        project.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThanOrEqualTo(firstSuccess);
        project.LastMaterializationError.ShouldNotBeNull();
        project.LastMaterializationError!.ShouldContain("local edits");
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        run.Refused.Length.ShouldBe(2);
        run.Refused.ShouldAllBe(r => r.Reason.Contains("local edits", StringComparison.Ordinal));
        run.Skipped.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Sync_TrackedFileHandEdited_ForceOverwritesAndWarnsPerFile()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        await File.AppendAllTextAsync(taskPath, "\nhand note\n");

        var report = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        report.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {taskPath}");
        report.Refused.ShouldBeEmpty();
        report.Skipped.Count.ShouldBe(3);
        var content = await File.ReadAllTextAsync(taskPath);
        content.ShouldNotContain("hand note");
        content.ShouldContain("# First Task");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_AllFilesRefused_AbortsAndStampsAttemptWithoutSuccess()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        DateTime firstStamp;
        DateTime firstAttempt;
        using (var seeded = _db.CreateContext())
        {
            var first = await seeded.Projects.SingleAsync(p => p.Id == projectId);
            firstStamp = first.LastMaterializedAt!.Value;
            firstAttempt = first.LastMaterializationAttemptAt!.Value;
        }

        var guarded = new List<(string Path, byte[] Bytes)>();
        foreach (var relative in new[]
                 {
                     "BACKLOG/006-demo/feature.md",
                     "BACKLOG/006-demo/spec.md",
                     "BACKLOG/006-demo/tasks/01-first-task.md",
                     "BACKLOG/006-demo/tasks/02-task-02.md",
                 })
        {
            var absolute = Path.Combine(_projectDir, relative);
            await File.AppendAllTextAsync(absolute, "\nhand edit\n");
            guarded.Add((absolute, await File.ReadAllBytesAsync(absolute)));
        }

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.OffendingPath.ShouldBeNull();
        ex.Message.ShouldContain("nothing was written");
        ex.Message.ShouldContain("planning_upsert_artifact");
        foreach (var relative in new[]
                 {
                     "BACKLOG/006-demo/feature.md",
                     "BACKLOG/006-demo/spec.md",
                     "BACKLOG/006-demo/tasks/01-first-task.md",
                     "BACKLOG/006-demo/tasks/02-task-02.md",
                 })
        {
            ex.Message.ShouldContain(relative);
        }

        foreach (var (absolute, bytes) in guarded)
        {
            (await File.ReadAllBytesAsync(absolute)).ShouldBe(bytes);
        }

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializedAt.ShouldBe(firstStamp);
        project.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThan(firstAttempt);
        project.LastMaterializationError.ShouldNotBeNull();
        project.LastMaterializationFailedAt.ShouldNotBeNull();
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run!.Atomic.ShouldBeFalse();
        run.Refused.Length.ShouldBe(4);
        run.Written.ShouldBeEmpty();
    }

    [Fact]
    public async Task BoardMutation_WhileTrackedFileDrifted_SucceedsAndLeavesDiskStale()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        await File.AppendAllTextAsync(taskPath, "\nsub-agent note\n");

        var task = await ctx.PlanningArtifacts.SingleAsync(a =>
            a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        var artifactService = new PlanningArtifactService(
            ctx,
            TestPlanningDispatcher.Create(ctx, materializer),
            new TaskDependencyService(ctx, TestPlanningDispatcher.Create(ctx, materializer)));
        await artifactService.AppendLogAsync(task.Id, "board-side note", CancellationToken.None);

        task.Body.ShouldContain("board-side note");
        var disk = await File.ReadAllTextAsync(taskPath);
        disk.ShouldContain("sub-agent note");
        disk.ShouldNotContain("board-side note");
        var drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        drift.ShouldHaveSingleItem().RelativePath.ShouldBe("BACKLOG/006-demo/tasks/01-first-task.md");
    }

    [Fact]
    public async Task Sync_BoardAbsorbedLocalEdit_WritesWithoutRefusalAndHealsRow()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        var edited = (await File.ReadAllTextAsync(taskPath))
            .Replace("Do it.", "Do it now.", StringComparison.Ordinal);
        await File.WriteAllTextAsync(taskPath, edited);

        var task = await ctx.PlanningArtifacts.SingleAsync(a =>
            a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.UpdateBody("# First Task\n\n<goal>\nDo it now.\n</goal>");
        await ctx.SaveChangesAsync();

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        (await File.ReadAllTextAsync(taskPath)).ShouldContain("Do it now.");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_LegacyIndexProseEdit_NeverBlocksTheStripPass()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, LegacyIndexFile("# BACKLOG\n\n", LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        await File.AppendAllTextAsync(indexPath, "\n## Human notes\n");

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        report.Refused.ShouldBeEmpty();
        report.Atomic.ShouldBeTrue();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe("# BACKLOG\n\n## Human notes\n");
        (await ctx.PlanningMaterializedFiles.CountAsync(m =>
                m.ProjectId == projectId && m.RelativePath == "BACKLOG.md"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Sync_TrackedFileDeletedLocally_IsRecreatedWithoutRefusal()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        File.Delete(taskPath);

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.Warnings.ShouldBeEmpty();
        (await File.ReadAllTextAsync(taskPath)).ShouldContain("# First Task");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Drift_TrackedFileDeletedOnDisk_ReportsMissing()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        File.Delete(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md"));

        var drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        var item = drift.ShouldHaveSingleItem();
        item.Kind.ShouldBe(DriftKind.Missing);
        item.RelativePath.ShouldBe("BACKLOG/006-demo/tasks/01-first-task.md");
        item.FeatureSlug.ShouldBe("006-demo");
    }

    [Fact]
    public async Task Archive_WritesUnderDataDirectoryAndRewritesFreely()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var archiveRoot = Path.Combine(_dataDir, "projects", "demo", "backlog");
        var featureFile = Path.Combine(archiveRoot, "BACKLOG", "006-demo", "feature.md");
        File.Exists(featureFile).ShouldBeTrue();
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeFalse();
        report.Strategy.ShouldBe(PlanningMaterialization.Archive);

        await File.WriteAllTextAsync(featureFile, "garbage\n");
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        (await File.ReadAllTextAsync(featureFile)).ShouldContain("feature: 006");

        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Off_ClearsFilesStripsLegacyIndexBlockAndDropsRows()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string prose = "# Human backlog\n\nkeep me\n";
        await File.WriteAllTextAsync(indexPath, LegacyIndexFile(prose, LegacyIndexBlock));

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);

        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.Off, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        using var ctx2 = _db.CreateContext();
        var report = await CreateMaterializer(ctx2).SyncAsync(projectId, ct: CancellationToken.None);

        report.Strategy.ShouldBe(PlanningMaterialization.Off);
        report.Files.Count.ShouldBe(5);
        report.Warnings.ShouldBeEmpty();

        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, "BACKLOG")).ShouldBeFalse();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(prose);
        (await ctx2.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task StrategySwitch_OnToArchive_ClearsRepoCopy()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string prose = "# Human backlog\n\nkeep me\n";

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        await File.WriteAllTextAsync(indexPath, LegacyIndexFile(prose, LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.Archive, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        using var ctx2 = _db.CreateContext();
        await CreateMaterializer(ctx2).SyncAsync(projectId, ct: CancellationToken.None);

        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, "BACKLOG")).ShouldBeFalse();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(prose);

        var archiveFile = Path.Combine(_dataDir, "projects", "demo", "backlog", "BACKLOG", "006-demo", "feature.md");
        File.Exists(archiveFile).ShouldBeTrue();

        var rows = await ctx2.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Count.ShouldBe(4);
        (await CreateMaterializer(ctx2).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StrategySwitch_WritePhaseFailure_OtherRootRowsDropAndPlannedRowsStayOut()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string prose = "# Human backlog\n\nkeep me\n";

        using (var firstCtx = _db.CreateContext())
        {
            await CreateMaterializer(firstCtx).SyncAsync(projectId, ct: CancellationToken.None);
            (await firstCtx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);
        }

        await File.WriteAllTextAsync(indexPath, LegacyIndexFile(prose, LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.Archive, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        var sabotagedDir = Path.Combine(_dataDir, "projects", "demo", "backlog", "BACKLOG", "006-demo");
        Directory.CreateDirectory(Path.GetDirectoryName(sabotagedDir)!);
        await File.WriteAllTextAsync(sabotagedDir, "a file where the archive feature directory belongs");

        using var failCtx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(failCtx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.IoError);

        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")).ShouldBeFalse();
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(prose);

        (await failCtx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(0);

        var failedProject = await failCtx.Projects.SingleAsync(p => p.Id == projectId);
        failedProject.LastMaterializationAttemptAt.ShouldNotBeNull();
        failedProject.LastMaterializationFailedAt.ShouldNotBeNull();
        failedProject.LastMaterializationError.ShouldNotBeNull();

        var run = MaterializationRunJson.Deserialize(failedProject.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        run.Refused.ShouldBeEmpty();
    }

    [Fact]
    public async Task StrategySwitch_ArchiveToOn_WithForeignRepoFile_RefusesThenForceAdopts()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var archiveMaterializer = CreateMaterializer(ctx);
        await archiveMaterializer.SyncAsync(projectId, ct: CancellationToken.None);

        var archiveFile = Path.Combine(_dataDir, "projects", "demo", "backlog", "BACKLOG", "006-demo", "feature.md");
        File.Exists(archiveFile).ShouldBeTrue();

        var repoFile = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(repoFile)!);
        await File.WriteAllTextAsync(repoFile, "user data");
        var repoBytes = await File.ReadAllBytesAsync(repoFile);

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        using var onCtx = _db.CreateContext();
        var onMaterializer = CreateMaterializer(onCtx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => onMaterializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        (await File.ReadAllBytesAsync(repoFile)).ShouldBe(repoBytes);
        File.Exists(archiveFile).ShouldBeTrue();
        (await onCtx.PlanningMaterializedFiles.CountAsync()).ShouldBe(4);

        var forced = await onMaterializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        forced.Warnings.ShouldHaveSingleItem();
        File.Exists(archiveFile).ShouldBeFalse();
        (await File.ReadAllTextAsync(repoFile)).ShouldContain("feature: 006");
        (await onCtx.PlanningMaterializedFiles.CountAsync()).ShouldBe(4);
        (await onMaterializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_ArchiveMode_ForeignRepoFileAtTrackedPath_IsKeptWithWarning()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var repoOriginal = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(repoOriginal)!);
        await File.WriteAllTextAsync(repoOriginal, "hand-written, never adopted");

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllTextAsync(repoOriginal)).ShouldBe("hand-written, never adopted");
        report.Warnings.ShouldHaveSingleItem().ShouldContain(repoOriginal);
        (await File.ReadAllTextAsync(Path.Combine(
                _dataDir, "projects", "demo", "backlog", "BACKLOG", "006-demo", "feature.md")))
            .ShouldContain("feature: 006");
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);
    }

    [Fact]
    public async Task StrategySwitch_ArchiveToOff_LegacyArchiveIndexCopyDeletedAndRepoProseKept()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        var archiveRoot = Path.Combine(_dataDir, "projects", "demo", "backlog");
        Directory.CreateDirectory(Path.Combine(archiveRoot, "BACKLOG", "006-demo"));
        var archiveFeature = Path.Combine(archiveRoot, "BACKLOG", "006-demo", "feature.md");
        const string archiveFeatureDoc = "archived feature document";
        await File.WriteAllTextAsync(
            Path.Combine(archiveRoot, "BACKLOG.md"),
            LegacyIndexFile("# Archived index\n\n", LegacyIndexBlock));
        await File.WriteAllTextAsync(archiveFeature, archiveFeatureDoc);

        var repoIndex = Path.Combine(_projectDir, "BACKLOG.md");
        const string repoProse = "# Human backlog\n\n- [ ] my own item\n";
        await File.WriteAllTextAsync(repoIndex, repoProse);

        var repoOriginal = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(repoOriginal)!);
        await File.WriteAllTextAsync(repoOriginal, "imported original, never adopted");

        await using (var seed = _db.CreateContext())
        {
            var featureId = await seed.PlanningFeatures
                .Where(f => f.ProjectId == projectId)
                .Select(f => f.Id)
                .SingleAsync();
            await SeedTrackedRowAsync(
                seed, projectId, featureId, "BACKLOG/006-demo/feature.md", ContentHash.Sha256(archiveFeatureDoc));
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.Off, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        using var ctx2 = _db.CreateContext();
        var report = await CreateMaterializer(ctx2).SyncAsync(projectId, ct: CancellationToken.None);

        report.Strategy.ShouldBe(PlanningMaterialization.Off);
        report.Files.Count.ShouldBe(2);
        report.Warnings.ShouldHaveSingleItem().ShouldContain(repoOriginal);

        (await File.ReadAllTextAsync(repoIndex)).ShouldBe(repoProse);
        (await File.ReadAllTextAsync(repoOriginal)).ShouldBe("imported original, never adopted");

        Directory.EnumerateFileSystemEntries(archiveRoot).ShouldBeEmpty();
        (await ctx2.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task StrategySwitch_ArchiveToOff_CleanRepo_ClearsEverything()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var archiveRoot = Path.Combine(_dataDir, "projects", "demo", "backlog");
        File.Exists(Path.Combine(archiveRoot, "BACKLOG.md")).ShouldBeFalse();

        using (var switchCtx = _db.CreateContext())
        {
            var project = await switchCtx.Projects.FirstAsync(p => p.Id == projectId);
            project.SetPlanningWorkflow(PlanningMaterialization.Off, HumanValidationOverride.Unset);
            await switchCtx.SaveChangesAsync();
        }

        using var ctx2 = _db.CreateContext();
        var report = await CreateMaterializer(ctx2).SyncAsync(projectId, ct: CancellationToken.None);

        report.Strategy.ShouldBe(PlanningMaterialization.Off);
        report.Files.Count.ShouldBe(4);
        report.Warnings.ShouldBeEmpty();
        Directory.EnumerateFileSystemEntries(archiveRoot).ShouldBeEmpty();
        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
        (await ctx2.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Sync_DuplicatedLeadingHeadings_CollapseToExactlyOne()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(
            projectId,
            "006-demo",
            "Demo Feature",
            "# Feature 006 — Demo Feature\n\n# Feature 006 — Demo Feature\n\n# Feature 006 — Demo Feature\n\n<goal>\nShip it.\n</goal>");
        feature.SetStatus(PlanningFeatureStatus.Ready);
        ctx.PlanningFeatures.Add(feature);

        var spec = PlanningArtifact.Create(
            feature.Id,
            PlanningArtifactKind.Spec,
            "# Spec 006 — Demo Feature\n\n# Spec 006 — Demo Feature\n\n<scope>\nSpec body.\n</scope>");
        var task = PlanningArtifact.Create(
            feature.Id,
            PlanningArtifactKind.Task,
            "# First Task\n\n# First Task\n\n<goal>\nDo it.\n</goal>",
            1);
        ctx.PlanningArtifacts.AddRange(spec, task);
        await ctx.SaveChangesAsync();

        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var featureDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md"));
        CountHeadings(featureDoc).ShouldBe(1);
        featureDoc.ShouldContain("# Feature 006 — Demo Feature\n\n<goal>");

        var specDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md"));
        CountHeadings(specDoc).ShouldBe(1);
        specDoc.ShouldContain("# Spec 006 — Demo Feature\n\n<scope>");

        var taskDoc = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md"));
        CountHeadings(taskDoc).ShouldBe(1);
        taskDoc.ShouldContain("# First Task\n\n<goal>");

        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")))
            .ShouldBe(featureDoc);
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")))
            .ShouldBe(specDoc);
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md")))
            .ShouldBe(taskDoc);
    }

    [Fact]
    public async Task Sync_RollbackAfterForceAdoption_KeepsPreExistingFiles()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var foreign = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user data");

        var tasksAsFile = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks");
        await File.WriteAllTextAsync(tasksAsFile, "a file where a directory is expected");

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, force: true, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.IoError);

        File.Exists(foreign).ShouldBeTrue();
        (await File.ReadAllTextAsync(foreign)).ShouldContain("feature: 006");
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
        (await ctx.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Sync_WritePhaseIoFailure_PersistsAttemptStampAndAbortedReport()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        DateTime firstAttempt;
        using (var seeded = _db.CreateContext())
        {
            firstAttempt = (await seeded.Projects.SingleAsync(p => p.Id == projectId))
                .LastMaterializationAttemptAt!.Value;
        }

        var feature = await ctx.PlanningFeatures.SingleAsync(f => f.ProjectId == projectId);
        feature.Update(null, "<goal>\nShip it v2.\n</goal>");
        await ctx.SaveChangesAsync();

        var featureDir = Path.Combine(_projectDir, "BACKLOG", "006-demo");
        Directory.Delete(featureDir, recursive: true);
        await File.WriteAllTextAsync(featureDir, "a file where the feature directory belongs");

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.IoError);
        ex.Message.ShouldContain(featureDir);

        await materializer.RecordSyncFailureAsync(projectId, ex.Message, CancellationToken.None);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThan(firstAttempt);
        project.LastMaterializationFailedAt.ShouldNotBeNull();
        project.LastMaterializationError.ShouldNotBeNull();
        project.LastMaterializationError!.ShouldContain("File operation failed");
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.StartedAt.ShouldNotBe(default);
        run.FinishedAt.ShouldBeGreaterThanOrEqualTo(run.StartedAt);
        run.Written.ShouldBeEmpty();
        run.Refused.ShouldBeEmpty();
        run.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_TraversalSlug_ThrowsAndWritesNothing()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedFeatureAsync(projectId, "../evil", "Evil", "body", PlanningFeatureStatus.Draft);

        using var ctx = _db.CreateContext();
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.PathTraversal);
        File.Exists(Path.Combine(_sandboxDir, "evil.md")).ShouldBeFalse();
        (await ctx.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Clear_UndeletableFile_SurfacesWarningAndDropsRow()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskDir = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks");
        var lockedFile = Path.Combine(taskDir, "01-first-task.md");
        File.SetUnixFileMode(taskDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            var report = await materializer.ClearAsync(projectId, ct: CancellationToken.None);

            report.Warnings.Count.ShouldBe(2);
            report.Warnings.ShouldAllBe(w => w.Contains(taskDir, StringComparison.Ordinal));
            File.Exists(lockedFile).ShouldBeTrue();

            File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeFalse();
            File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
            (await ctx.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
        }
        finally
        {
            File.SetUnixFileMode(taskDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task Sync_TaskTitleRename_KeepsPinnedFilenameAndCleanDrift()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var task = await ctx.PlanningArtifacts.SingleAsync(a =>
            a.Kind == PlanningArtifactKind.Task && a.Order == 1);
        task.UpdateBody("# Renamed Task\n\n<goal>\nDo it.\n</goal>");
        await ctx.SaveChangesAsync();

        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var tasksDir = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks");
        File.Exists(Path.Combine(tasksDir, "01-first-task.md")).ShouldBeTrue();
        File.Exists(Path.Combine(tasksDir, "01-renamed-task.md")).ShouldBeFalse();
        Directory.GetFiles(tasksDir).Length.ShouldBe(2);
        (await File.ReadAllTextAsync(Path.Combine(tasksDir, "01-first-task.md"))).ShouldContain("# Renamed Task");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_CyrillicTaskTitleRename_KeepsPinnedFilenameAndCleanDrift()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);

        using var seedCtx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        seedCtx.PlanningFeatures.Add(feature);
        seedCtx.PlanningArtifacts.Add(PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# Первая задача\n\n<goal>\nDo it.\n</goal>", 1));
        await seedCtx.SaveChangesAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var tasksDir = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks");
        var pinnedPath = Path.Combine(tasksDir, "01-pervaya-zadacha.md");
        File.Exists(pinnedPath).ShouldBeTrue();

        var task = await ctx.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
        task.UpdateBody("# Вторая задача\n\n<goal>\nDo it.\n</goal>");
        await ctx.SaveChangesAsync();

        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        File.Exists(pinnedPath).ShouldBeTrue();
        File.Exists(Path.Combine(tasksDir, "01-вторая-задача.md")).ShouldBeFalse();
        Directory.GetFiles(tasksDir).Length.ShouldBe(1);
        (await File.ReadAllTextAsync(pinnedPath)).ShouldContain("# Вторая задача");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_PreservedFrontmatter_EmittedSortedOrdinalAfterStructuralKeys()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);

        using var seedCtx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        seedCtx.PlanningFeatures.Add(feature);
        var task = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# First Task\n\n<goal>\nDo it.\n</goal>", 1);
        task.SetPreservedFrontmatter("{\"risk_level\":\"high\",\"depends_on\":\"02-other\"}");
        seedCtx.PlanningArtifacts.Add(task);
        await seedCtx.SaveChangesAsync();

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var doc = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md"));
        doc.ShouldContain(
            $"created: {DateTime.UtcNow:yyyy-MM-dd}\n" +
            "depends_on: 02-other\n" +
            "risk_level: high\n" +
            "---\n\n" +
            "# First Task");
    }

    private static int CountHeadings(string content) =>
        content.Split('\n').Count(line => line.StartsWith("# ", StringComparison.Ordinal));

    private static async Task<Dictionary<string, string>> SnapshotTreeAsync(string root)
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            snapshot[file[root.Length..].Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(bytes));
        }

        return snapshot;
    }

    [Fact]
    public async Task Sync_Success_StampsAttemptLastMaterializedAndRunReport()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using (var seed = _db.CreateContext())
        {
            var fresh = await seed.Projects.SingleAsync(p => p.Id == projectId);
            fresh.LastMaterializedAt.ShouldBeNull();
            fresh.LastMaterializationAttemptAt.ShouldBeNull();
            fresh.LastMaterializationRun.ShouldBeNull();
        }

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializedAt.ShouldNotBeNull();
        project.LastMaterializationAttemptAt.ShouldNotBeNull();
        project.LastMaterializationAttemptAt!.Value.ShouldBeLessThanOrEqualTo(project.LastMaterializedAt!.Value);
        project.LastMaterializationError.ShouldBeNull();

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeTrue();
        run.Written.Length.ShouldBe(4);
        run.Skipped.ShouldBeEmpty();
        run.Refused.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_OffStrategy_DoesNotStamp()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Off);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializedAt.ShouldBeNull();
        project.LastMaterializationAttemptAt.ShouldBeNull();
        project.LastMaterializationRun.ShouldBeNull();
    }

    [Fact]
    public async Task Sync_RerunWithoutBoardChanges_SkipsEverythingAndStampsSuccess()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        DateTime firstStamp;
        using (var seeded = _db.CreateContext())
        {
            firstStamp = (await seeded.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt!.Value;
        }

        var before = await SnapshotTreeAsync(_projectDir);
        var rerun = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        rerun.Atomic.ShouldBeTrue();
        rerun.Files.ShouldBeEmpty();
        rerun.Refused.ShouldBeEmpty();
        rerun.Skipped.Count.ShouldBe(4);
        rerun.Skipped.ShouldAllBe(s => s.Reason.Contains("already matches", StringComparison.Ordinal));
        (await SnapshotTreeAsync(_projectDir)).ShouldBe(before);

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializedAt!.Value.ShouldBeGreaterThan(firstStamp);
        project.LastMaterializationError.ShouldBeNull();
        project.LastMaterializationSkipCount.ShouldBe(4);
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(4);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_FailedRunThenSuccess_SplitsAttemptFromSuccessStamps()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        DateTime firstStamp;
        DateTime firstAttempt;
        using (var seeded = _db.CreateContext())
        {
            var first = await seeded.Projects.SingleAsync(p => p.Id == projectId);
            firstStamp = first.LastMaterializedAt!.Value;
            firstAttempt = first.LastMaterializationAttemptAt!.Value;
        }

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        await File.AppendAllTextAsync(taskPath, "\nhand edit\n");

        await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        using (var failed = _db.CreateContext())
        {
            var project = await failed.Projects.SingleAsync(p => p.Id == projectId);
            project.LastMaterializedAt.ShouldBe(firstStamp);
            project.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThan(firstAttempt);
            project.LastMaterializationError.ShouldNotBeNull();
            project.LastMaterializationError!.ShouldContain("local edits");
            project.LastMaterializationFailedAt.ShouldNotBeNull();
        }

        var report = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        report.Warnings.ShouldHaveSingleItem().ShouldContain(taskPath);
        report.Refused.ShouldBeEmpty();
        report.Atomic.ShouldBeTrue();
        report.Skipped.Count.ShouldBe(3);

        using var verify = _db.CreateContext();
        var stamped = await verify.Projects.SingleAsync(p => p.Id == projectId);
        stamped.LastMaterializedAt!.Value.ShouldBeGreaterThan(firstStamp);
        stamped.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThanOrEqualTo(report.StartedAt);
        stamped.LastMaterializationError.ShouldBeNull();
        stamped.LastMaterializationFailedAt.ShouldBeNull();
        stamped.LastMaterializationSkipCount.ShouldBe(3);
        stamped.LastMaterializationSkipHead.ShouldNotBeNull();
    }

    [Fact]
    public async Task Sync_RunReportJson_RoundTripsPerFileOutcomes()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        await File.AppendAllTextAsync(taskPath, "\nhand edit\n");

        await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        using var verify = _db.CreateContext();
        var project = await verify.Projects.SingleAsync(p => p.Id == projectId);

        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        run.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.StartedAt.ShouldNotBe(default);
        run.FinishedAt.ShouldBeGreaterThanOrEqualTo(run.StartedAt);
        run.Written.ShouldBeEmpty();

        var refusal = run.Refused.ShouldHaveSingleItem();
        refusal.RelativePath.ShouldBe("BACKLOG/006-demo/tasks/01-first-task.md");
        refusal.Reason.ShouldContain("local edits");

        run.Skipped.Length.ShouldBe(3);
        run.Skipped.ShouldContain(s =>
            s.RelativePath == "BACKLOG/006-demo/spec.md" &&
            s.Reason.Contains("already matches", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sync_LegacyIndexBlockHandEdited_RefusesThenForceKeepsForeignWithWarning()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, LegacyIndexFile("# BACKLOG\n\n", LegacyIndexBlock));
        await using (var seed = _db.CreateContext())
        {
            await SeedTrackedRowAsync(seed, projectId, featureId: null, "BACKLOG.md", ContentHash.Sha256(LegacyIndexBlock));
        }

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var edited = (await File.ReadAllTextAsync(indexPath)).Replace("`[ ]`", "`[x]`", StringComparison.Ordinal);
        await File.WriteAllTextAsync(indexPath, edited);
        var editedBytes = await File.ReadAllBytesAsync(indexPath);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        ex.OffendingPath.ShouldBe("BACKLOG.md");
        ex.Message.ShouldContain("- BACKLOG.md: tracked file has local edits that are not on the board");
        ex.Message.ShouldNotContain(indexPath);
        (await File.ReadAllBytesAsync(indexPath)).ShouldBe(editedBytes);
        (await ctx.PlanningMaterializedFiles.CountAsync(m =>
                m.ProjectId == projectId && m.RelativePath == "BACKLOG.md"))
            .ShouldBe(1);

        var forced = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        forced.Refused.ShouldBeEmpty();
        forced.Warnings.ShouldHaveSingleItem().ShouldBe($"Kept foreign file at a tracked path: {indexPath}");
        (await File.ReadAllBytesAsync(indexPath)).ShouldBe(editedBytes);
        (await ctx.PlanningMaterializedFiles.CountAsync(m =>
                m.ProjectId == projectId && m.RelativePath == "BACKLOG.md"))
            .ShouldBe(1);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None))
            .ShouldHaveSingleItem().Kind.ShouldBe(DriftKind.Modified);
    }

    private const string LegacyIndexBlock = "- `[ ]` 006-demo → `BACKLOG/006-demo/` — Demo Feature";

    private static string LegacyIndexFile(string prose, string block) =>
        $"{prose}<!-- overmind:planning:start -->\n{block}\n<!-- overmind:planning:end -->\n";

    private static async Task SeedTrackedRowAsync(
        OvermindDbContext ctx, Guid projectId, Guid? featureId, string relativePath, string sha256)
    {
        ctx.PlanningMaterializedFiles.Add(
            PlanningMaterializedFile.Create(projectId, featureId, relativePath, sha256));
        await ctx.SaveChangesAsync();
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

    private async Task SeedDefaultFeatureAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        feature.SetStatus(PlanningFeatureStatus.Ready);
        ctx.PlanningFeatures.Add(feature);

        var spec = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Spec, "# Spec Heading\n\n<scope>\nSpec body.\n</scope>");
        var taskOne = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# First Task\n\n<goal>\nDo it.\n</goal>", 1);
        taskOne.SetReview(PlanningReviewLevel.Hard);
        var taskTwo = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "no heading here", 2);

        ctx.PlanningArtifacts.AddRange(spec, taskOne, taskTwo);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedFeatureAsync(
        Guid projectId, string slug, string title, string body, PlanningFeatureStatus status)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, slug, title, body);
        feature.SetStatus(status);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    [Fact]
    public async Task Sync_DeletedArtifact_RemovesItsFileOnTheNextPassWithoutTombstone()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using (var seed = _db.CreateContext())
        {
            var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "body");
            var task = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "# Doomed Task\n\n<goal>\nDie.\n</goal>", 0);
            seed.PlanningFeatures.Add(feature);
            seed.PlanningArtifacts.Add(task);
            await seed.SaveChangesAsync();
        }

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "00-doomed-task.md");
        using (var sync = _db.CreateContext())
        {
            await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        File.Exists(taskPath).ShouldBeTrue();

        using (var delete = _db.CreateContext())
        {
            var task = await delete.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
            task.Delete(byHuman: false);
            await delete.SaveChangesAsync();
        }

        using (var resync = _db.CreateContext())
        {
            var report = await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);

            report.Warnings.ShouldBeEmpty();
            report.Refused.ShouldBeEmpty();
        }

        File.Exists(taskPath).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks")).ShouldBeFalse();

        using (var verify = _db.CreateContext())
        {
            var rows = await verify.PlanningMaterializedFiles
                .Where(m => m.ProjectId == projectId && m.RelativePath.Contains("00-doomed-task"))
                .ToListAsync();
            rows.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Sync_DeletedArtifactWithHandEdits_RefusesThePassAndKeepsTheFile()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "00-doomed-task.md");
        using (var seed = _db.CreateContext())
        {
            var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "body");
            var task = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Task, "# Doomed Task\n\n<goal>\nDie.\n</goal>", 0);
            seed.PlanningFeatures.Add(feature);
            seed.PlanningArtifacts.Add(task);
            await seed.SaveChangesAsync();
        }

        using (var sync = _db.CreateContext())
        {
            await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        using (var delete = _db.CreateContext())
        {
            var task = await delete.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
            task.Delete(byHuman: true);
            await delete.SaveChangesAsync();
        }

        await File.WriteAllTextAsync(taskPath, "# Hand edited after the sync\n");

        using (var refuse = _db.CreateContext())
        {
            var ex = await Should.ThrowAsync<MaterializationException>(() =>
                CreateMaterializer(refuse).SyncAsync(projectId, ct: CancellationToken.None));
            ex.Kind.ShouldBe(MaterializationFailureKind.Refused);
        }

        File.Exists(taskPath).ShouldBeTrue();

        using (var forced = _db.CreateContext())
        {
            var report = await CreateMaterializer(forced).SyncAsync(projectId, force: true, ct: CancellationToken.None);

            report.Warnings.ShouldContain(w => w.Contains("00-doomed-task.md"));
        }

        File.Exists(taskPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Sync_DeletedDocument_SpecFileLeavesFeatureFolderIntact()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        using (var seed = _db.CreateContext())
        {
            var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "body");
            var spec = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Spec, "# Spec\n\n<scope>\nbody\n</scope>");
            seed.PlanningFeatures.Add(feature);
            seed.PlanningArtifacts.Add(spec);
            await seed.SaveChangesAsync();
        }

        using (var sync = _db.CreateContext())
        {
            await CreateMaterializer(sync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        using (var delete = _db.CreateContext())
        {
            var spec = await delete.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Spec);
            spec.Delete(byHuman: true);
            await delete.SaveChangesAsync();
        }

        using (var resync = _db.CreateContext())
        {
            await CreateMaterializer(resync).SyncAsync(projectId, ct: CancellationToken.None);
        }

        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeFalse();
    }
}
