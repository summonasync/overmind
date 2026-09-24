using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
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

        var index = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md"));
        index.ShouldBe(
            "# BACKLOG\n\n" +
            "<!-- overmind:planning:start -->\n" +
            "- `[ ]` 006-demo → `BACKLOG/006-demo/` — Demo Feature\n" +
            "- `[x]` auth → `BACKLOG/auth/` — Auth\n" +
            "<!-- overmind:planning:end -->\n");

        var rows = await ctx.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Select(r => r.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ShouldBe(
            new[]
            {
                "BACKLOG.md",
                "BACKLOG/006-demo/feature.md",
                "BACKLOG/006-demo/spec.md",
                "BACKLOG/006-demo/tasks/01-first-task.md",
                "BACKLOG/006-demo/tasks/02-task-02.md",
                "BACKLOG/auth/feature.md",
            });

        report.Files.Count.ShouldBe(6);
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
        File.Exists(Path.Combine(tasksDir, "02-cpm-обновление-пакетов-aspire-13-5.md")).ShouldBeTrue();
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
    public async Task On_ReviewFeature_EmitsReviewStatusAndTildeMarker()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedFeatureAsync(projectId, "audit", "Audit", "body", PlanningFeatureStatus.Review);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var featureDoc = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "audit", "feature.md"));
        featureDoc.ShouldContain("status: review\n");
        featureDoc.ShouldNotContain("status: inProgress");

        var index = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md"));
        index.ShouldContain("- `[~]` audit → `BACKLOG/audit/` — Audit");
    }

    [Fact]
    public async Task Resync_PreservesForeignContentOutsideBlock_ByteForByte()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string humanContent = "# My BACKLOG\n\nHand-written intro.\n## Deferred\n- later";
        await File.WriteAllTextAsync(indexPath, humanContent);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var first = await File.ReadAllTextAsync(indexPath);
        first.ShouldBe(humanContent + "\n\n<!-- overmind:planning:start -->\n- `[ ]` 006-demo → `BACKLOG/006-demo/` — Demo Feature\n<!-- overmind:planning:end -->\n");
        var preservedPrefix = first[..first.IndexOf("<!-- overmind:planning:start -->", StringComparison.Ordinal)];

        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "006-demo");
        feature.Update(null, "<goal>\nShip it v2.\n</goal>");
        await ctx.SaveChangesAsync();

        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var second = await File.ReadAllTextAsync(indexPath);
        var secondPrefix = second[..second.IndexOf("<!-- overmind:planning:start -->", StringComparison.Ordinal)];
        secondPrefix.ShouldBe(preservedPrefix);
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")))
            .ShouldContain("Ship it v2.");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_AppendsBlockWhenMarkersAbsent()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, "# BACKLOG\n\n- [ ] human item\n");

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var content = await File.ReadAllTextAsync(indexPath);
        content.ShouldBe(
            "# BACKLOG\n\n- [ ] human item\n" +
            "\n<!-- overmind:planning:start -->\n" +
            "- `[ ]` 006-demo → `BACKLOG/006-demo/` — Demo Feature\n" +
            "<!-- overmind:planning:end -->\n");
    }

    [Fact]
    public async Task Sync_CreatesBacklogFileWhenMissing()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var content = await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md"));
        content.ShouldBe(
            "# BACKLOG\n\n<!-- overmind:planning:start -->\n" +
            "- `[ ]` 006-demo → `BACKLOG/006-demo/` — Demo Feature\n" +
            "<!-- overmind:planning:end -->\n");
    }

    [Fact]
    public async Task Drift_BacklogIsScopedToBlockOnly()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
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
    public async Task Sync_UntrackedExistingFile_SkipsItWritesSiblingsAndKeepsBytes()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var foreign = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user data");
        var foreignBytes = await File.ReadAllBytesAsync(foreign);

        using var ctx = _db.CreateContext();
        var report = await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllBytesAsync(foreign)).ShouldBe(foreignBytes);

        var skip = report.Skipped.ShouldHaveSingleItem();
        skip.RelativePath.ShouldBe("BACKLOG/006-demo/feature.md");
        skip.Reason.ShouldContain("not tracked by Overmind");
        report.Files.Count.ShouldBe(4);
        report.Files.ShouldNotContain(f => f.RelativePath == "BACKLOG/006-demo/feature.md");

        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")))
            .ShouldEndWith("<scope>\nSpec body.\n</scope>");
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md")).ShouldBeTrue();
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md")))
            .ShouldContain("`[ ]` 006-demo");

        var rows = await ctx.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.RelativePath)
            .ToListAsync();
        rows.ShouldNotContain("BACKLOG/006-demo/feature.md");
        rows.Count.ShouldBe(4);
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
    public async Task Sync_TrackedFilesHandEdited_SkipsBothWithReasonsAndKeepsBytes()
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
        var taskBytes = await File.ReadAllBytesAsync(taskPath);
        var specBytes = await File.ReadAllBytesAsync(specPath);

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllBytesAsync(taskPath)).ShouldBe(taskBytes);
        (await File.ReadAllBytesAsync(specPath)).ShouldBe(specBytes);

        report.Skipped.Count.ShouldBe(2);
        report.Skipped.ShouldContain(s =>
            s.RelativePath == "BACKLOG/006-demo/tasks/01-first-task.md" &&
            s.Reason.Contains("local edits", StringComparison.Ordinal));
        report.Skipped.ShouldContain(s =>
            s.RelativePath == "BACKLOG/006-demo/spec.md" &&
            s.Reason.Contains("local edits", StringComparison.Ordinal));
        report.Files.Count.ShouldBe(3);
        report.Files.ShouldContain(f => f.RelativePath == "BACKLOG/006-demo/feature.md");
        report.Files.ShouldContain(f => f.RelativePath == "BACKLOG.md");

        var drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        drift.Count.ShouldBe(2);
        drift.ShouldAllBe(d => d.Kind == DriftKind.Modified);
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
        report.Skipped.ShouldBeEmpty();
        var content = await File.ReadAllTextAsync(taskPath);
        content.ShouldNotContain("hand note");
        content.ShouldContain("# First Task");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_AllPlansGuarded_ThrowsAllSkippedWithoutStamping()
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

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.WriteAllTextAsync(indexPath, (await File.ReadAllTextAsync(indexPath))
            .Replace("`[ ]`", "`[x]`", StringComparison.Ordinal));
        guarded.Add((indexPath, await File.ReadAllBytesAsync(indexPath)));

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.AllSkipped);
        ex.OffendingPath.ShouldBeNull();
        ex.Message.ShouldContain("nothing was written");
        ex.Message.ShouldContain("planning_upsert_artifact");
        foreach (var relative in new[]
                 {
                     "BACKLOG.md",
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
        project.LastMaterializationError.ShouldBeNull();
        (await verify.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(5);
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
            ctx, materializer, NullLogger<PlanningArtifactService>.Instance);
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
    public async Task Sync_BacklogEditOutsideManagedBlock_NeverBlocks_InsideBlockEdit_SkipsIndexOnly()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        await File.AppendAllTextAsync(indexPath, "\n## Human notes\n");

        var outside = await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        outside.Warnings.ShouldBeEmpty();
        outside.Skipped.ShouldBeEmpty();
        (await File.ReadAllTextAsync(indexPath)).ShouldContain("## Human notes");

        var edited = (await File.ReadAllTextAsync(indexPath))
            .Replace("`[ ]`", "`[x]`", StringComparison.Ordinal);
        await File.WriteAllTextAsync(indexPath, edited);
        var editedBytes = await File.ReadAllBytesAsync(indexPath);

        var partial = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllBytesAsync(indexPath)).ShouldBe(editedBytes);
        var skip = partial.Skipped.ShouldHaveSingleItem();
        skip.RelativePath.ShouldBe("BACKLOG.md");
        skip.Reason.ShouldContain("local edits");
        partial.Files.Count.ShouldBe(4);
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md")))
            .ShouldContain("Demo Feature");

        var forced = await materializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);
        forced.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {indexPath}");

        var index = await File.ReadAllTextAsync(indexPath);
        index.ShouldContain("`[ ]` 006-demo");
        index.ShouldContain("## Human notes");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
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
    public async Task Off_ClearsFilesPrunesDirectoriesRemovesBlockAndDropsRows()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string humanContent = "# Human backlog\n\nkeep me\n";
        await File.WriteAllTextAsync(indexPath, humanContent);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        (await ctx.PlanningMaterializedFiles.CountAsync()).ShouldBe(5);

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
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(humanContent);
        (await ctx2.PlanningMaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task StrategySwitch_OnToArchive_ClearsRepoCopy()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        const string humanContent = "# Human backlog\n\nkeep me\n";
        await File.WriteAllTextAsync(indexPath, humanContent);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

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
        (await File.ReadAllTextAsync(indexPath)).ShouldBe(humanContent);

        var archiveFile = Path.Combine(_dataDir, "projects", "demo", "backlog", "BACKLOG", "006-demo", "feature.md");
        File.Exists(archiveFile).ShouldBeTrue();

        var rows = await ctx2.PlanningMaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Count.ShouldBe(5);
        (await CreateMaterializer(ctx2).DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StrategySwitch_ArchiveToOn_WithForeignRepoFile_SkipsItAndWritesTheRest()
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

        var report = await onMaterializer.SyncAsync(projectId, ct: CancellationToken.None);

        (await File.ReadAllBytesAsync(repoFile)).ShouldBe(repoBytes);
        var skip = report.Skipped.ShouldHaveSingleItem();
        skip.RelativePath.ShouldBe("BACKLOG/006-demo/feature.md");
        skip.Reason.ShouldContain("not tracked by Overmind");
        report.Files.ShouldNotContain(f => f.RelativePath == "BACKLOG/006-demo/feature.md");
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "spec.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_projectDir, "BACKLOG.md")).ShouldBeTrue();
        File.Exists(archiveFile).ShouldBeFalse();
        (await onCtx.PlanningMaterializedFiles.CountAsync()).ShouldBe(4);

        var forced = await onMaterializer.SyncAsync(projectId, force: true, ct: CancellationToken.None);

        forced.Warnings.ShouldHaveSingleItem();
        File.Exists(archiveFile).ShouldBeFalse();
        (await File.ReadAllTextAsync(repoFile)).ShouldContain("feature: 006");
        (await onCtx.PlanningMaterializedFiles.CountAsync()).ShouldBe(5);
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
        (await ctx.PlanningMaterializedFiles.CountAsync(m => m.ProjectId == projectId)).ShouldBe(5);
    }

    [Fact]
    public async Task StrategySwitch_ArchiveToOff_RemovesArchiveIndexAndKeepsForeignRepoFiles()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Archive);
        await SeedDefaultFeatureAsync(projectId);

        var repoIndex = Path.Combine(_projectDir, "BACKLOG.md");
        const string humanIndex = "# Human backlog\n\n- [ ] my own item\n";
        await File.WriteAllTextAsync(repoIndex, humanIndex);

        var repoOriginal = Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(repoOriginal)!);
        await File.WriteAllTextAsync(repoOriginal, "imported original, never adopted");

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

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
        report.Warnings.ShouldHaveSingleItem().ShouldContain(repoOriginal);

        (await File.ReadAllTextAsync(repoIndex)).ShouldBe(humanIndex);
        (await File.ReadAllTextAsync(repoOriginal)).ShouldBe("imported original, never adopted");

        var archiveRoot = Path.Combine(_dataDir, "projects", "demo", "backlog");
        File.Exists(Path.Combine(archiveRoot, "BACKLOG.md")).ShouldBeFalse();
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
        File.Exists(Path.Combine(archiveRoot, "BACKLOG.md")).ShouldBeTrue();

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
            (await File.ReadAllTextAsync(Path.Combine(_projectDir, "BACKLOG.md")))
                .ShouldBe("# BACKLOG\n");
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
        var pinnedPath = Path.Combine(tasksDir, "01-первая-задача.md");
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

    [Fact]
    public async Task Sync_Success_StampsLastMaterializedAt()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using (var seed = _db.CreateContext())
        {
            (await seed.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt.ShouldBeNull();
        }

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        using var verify = _db.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Sync_OffStrategy_DoesNotStamp()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.Off);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        using var verify = _db.CreateContext();
        (await verify.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Sync_PartialSkip_StillStampsLastMaterializedAt()
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

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        await File.AppendAllTextAsync(taskPath, "\nhand edit\n");

        var report = await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        report.Skipped.ShouldHaveSingleItem().RelativePath.ShouldBe("BACKLOG/006-demo/tasks/01-first-task.md");
        (await File.ReadAllTextAsync(taskPath)).ShouldContain("hand edit");

        using var verify = _db.CreateContext();
        var stamped = await verify.Projects.SingleAsync(p => p.Id == projectId);
        stamped.LastMaterializedAt!.Value.ShouldBeGreaterThan(firstStamp);
        stamped.LastMaterializationSkipCount.ShouldBe(1);
        stamped.LastMaterializationSkipHead.ShouldBe(
            "BACKLOG/006-demo/tasks/01-first-task.md: tracked file has local edits that are not on the board");
    }

    [Fact]
    public async Task Sync_AllPlansGuardedSingleSkip_OffendingPathStaysRelative()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var indexPath = Path.Combine(_projectDir, "BACKLOG.md");
        var seededIndex = await File.ReadAllTextAsync(indexPath);
        await File.WriteAllTextAsync(indexPath, seededIndex.Replace(
            "planning:start -->\n\n<!-- overmind:planning:end -->",
            "planning:start -->\nstray line inside the managed block\n<!-- overmind:planning:end -->",
            StringComparison.Ordinal));

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.AllSkipped);
        ex.OffendingPath.ShouldBe("BACKLOG.md");
        ex.Message.ShouldContain("- BACKLOG.md: tracked file has local edits that are not on the board");
        ex.Message.ShouldNotContain(indexPath);
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
}
