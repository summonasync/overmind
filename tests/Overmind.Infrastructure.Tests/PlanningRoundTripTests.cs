using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningRoundTripTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _repoDir;
    private readonly string _dataDir;

    public PlanningRoundTripTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-rt-{Guid.NewGuid():N}");
        _repoDir = Path.Combine(_sandboxDir, "repo");
        _dataDir = Path.Combine(_sandboxDir, "data");
        Directory.CreateDirectory(BacklogDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    private string BacklogDir => Path.Combine(_repoDir, "BACKLOG");

    [Fact]
    public async Task RoundTrip_MaterializeIsByteIdempotentAndReImportIsNoOp()
    {
        var projectId = await SeedProjectAsync();
        WriteFullFeature();

        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        var first = await SnapshotAsync();

        await SyncAsync(projectId);

        (await SnapshotAsync()).ShouldBe(first);

        var reimport = await ImportAsync(projectId);

        reimport.FeaturesCreated.ShouldBe(0);
        reimport.ArtifactsCreated.ShouldBe(0);
        reimport.ArtifactsUpdated.ShouldBe(0);
        (await SnapshotAsync()).ShouldBe(first);

        var specDoc = first["BACKLOG/010-demo/spec.md"];
        CountHeadings(specDoc).ShouldBe(1);
        specDoc.ShouldContain("# Spec 010 — Demo feature title\n\n<scope>");
        specDoc.ShouldContain("validated: 2026-09-02\n");
    }

    [Fact]
    public async Task RoundTrip_ValidatedSpec_KeepsFrozenAtAndNoSpuriousEdit()
    {
        var projectId = await SeedProjectAsync();
        WriteFullFeature();

        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        var reimport = await ImportAsync(projectId);

        reimport.ArtifactsCreated.ShouldBe(0);
        reimport.ArtifactsUpdated.ShouldBe(0);

        using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        var spec = feature.Artifacts.Single(a => a.Kind == PlanningArtifactKind.Spec);
        spec.Status.ShouldBe(PlanningArtifactStatus.Validated);
        spec.FrozenAt.ShouldBe(new DateTime(2026, 9, 2));
        spec.Version.ShouldBe(1);
        spec.Body.ShouldBe(
            "<scope>\nSpec body.\n</scope>\n\n<log>\n- 2026-09-01 — brainstormed.\n</log>");
    }

    [Fact]
    public async Task RoundTrip_BriefedTask_SurvivesWipeAndReImportAsTypedField()
    {
        var projectId = await SeedProjectAsync();
        WriteBriefedFeature();

        await ImportAsync(projectId);
        using (var ctx = _db.CreateContext())
        {
            var task = await ctx.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
            task.Brief.ShouldBe("Wire the polling hook");
            task.PreservedFrontmatter.ShouldNotBeNull();
            task.PreservedFrontmatter.ShouldNotContain("brief");
            task.PreservedFrontmatter.ShouldContain("owner");
        }

        await SyncAsync(projectId, force: true);

        var taskPath = Path.Combine(BacklogDir, "010-demo", "tasks", "01-brief-task.md");
        var materialized = await File.ReadAllTextAsync(taskPath);
        materialized.ShouldContain("created: 2026-09-01\nbrief: Wire the polling hook\nowner: bob\n");

        var first = await SnapshotAsync();

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures
                .Include(f => f.Artifacts)
                .FirstAsync(f => f.ProjectId == projectId);
            ctx.PlanningFeatures.Remove(feature);
            await ctx.SaveChangesAsync();
        }

        var reimport = await ImportAsync(projectId);

        reimport.FeaturesCreated.ShouldBe(1);
        reimport.ArtifactsCreated.ShouldBe(1);

        using (var ctx = _db.CreateContext())
        {
            var task = await ctx.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
            task.Brief.ShouldBe("Wire the polling hook");
            task.PreservedFrontmatter.ShouldNotBeNull();
            task.PreservedFrontmatter.ShouldNotContain("brief");
        }

        (await SnapshotAsync()).ShouldBe(first);
    }

    [Fact]
    public async Task RoundTrip_TaskFileWithoutBrief_ImportsNullAndFlagsNoDrift()
    {
        var projectId = await SeedProjectAsync();
        WriteFullFeature();

        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        var taskPath = Path.Combine(BacklogDir, "010-demo", "tasks", "01-first-task.md");
        (await File.ReadAllTextAsync(taskPath)).ShouldNotContain("brief:");

        var reimport = await ImportAsync(projectId);

        reimport.FeaturesCreated.ShouldBe(0);
        reimport.ArtifactsCreated.ShouldBe(0);
        reimport.ArtifactsUpdated.ShouldBe(0);

        using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures
            .Include(f => f.Artifacts)
            .FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        feature.Artifacts.ShouldAllBe(a => a.Brief == null);
    }

    [Fact]
    public async Task RoundTrip_Priority_SurvivesMaterializeWipeAndReImport()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature("priority: P1");

        await ImportAsync(projectId);
        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
            feature.Priority.ShouldBe(FeaturePriority.P1);
        }

        await SyncAsync(projectId, force: true);

        var featurePath = Path.Combine(BacklogDir, "010-demo", "feature.md");
        (await File.ReadAllTextAsync(featurePath)).ShouldContain("status: ready\npriority: p1\nversion: 3\n");

        var first = await SnapshotAsync();

        await SyncAsync(projectId);

        (await SnapshotAsync()).ShouldBe(first);

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures
                .Include(f => f.Artifacts)
                .FirstAsync(f => f.ProjectId == projectId);
            ctx.PlanningFeatures.Remove(feature);
            await ctx.SaveChangesAsync();
        }

        var reimport = await ImportAsync(projectId);

        reimport.FeaturesCreated.ShouldBe(1);
        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
            feature.Priority.ShouldBe(FeaturePriority.P1);
        }

        (await SnapshotAsync()).ShouldBe(first);
    }

    [Fact]
    public async Task RoundTrip_DependencyLinks_ExportThenImportIntoFreshDatabase_RestoresLinks()
    {
        var projectId = await SeedProjectAsync();
        using (var ctx = _db.CreateContext())
        {
            var auth = PlanningFeature.Create(projectId, "020-auth", "Auth", "<goal>\nAuth.\n</goal>");
            var storage = PlanningFeature.Create(projectId, "030-storage", "Storage", "<goal>\nStorage.\n</goal>");
            var demo = PlanningFeature.Create(projectId, "010-demo", "Demo", "<goal>\nDemo.\n</goal>");
            demo.AddBlockedBy(storage);
            demo.AddBlockedBy(auth);
            ctx.PlanningFeatures.AddRange(auth, storage, demo);
            await ctx.SaveChangesAsync();
        }

        await SyncAsync(projectId, force: true);

        var demoDoc = await File.ReadAllTextAsync(Path.Combine(BacklogDir, "010-demo", "feature.md"));
        demoDoc.ShouldContain("depends_on: 020-auth, 030-storage\n");
        (await File.ReadAllTextAsync(Path.Combine(BacklogDir, "020-auth", "feature.md")))
            .ShouldNotContain("depends_on");

        var exported = await SnapshotAsync();

        using var freshDb = new TempDatabase();
        Guid freshProjectId;
        using (var ctx = freshDb.CreateContext())
        {
            var project = Project.Create($"rt-fresh-{Guid.NewGuid():N}", _repoDir, string.Empty);
            project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            freshProjectId = project.Id;
        }

        BacklogImportReport report;
        using (var ctx = freshDb.CreateContext())
        {
            report = await CreateImporter(ctx)
                .ImportAsync(new BacklogImportRequest(_repoDir, freshProjectId), CancellationToken.None);
        }

        report.FeaturesCreated.ShouldBe(3);
        report.Skipped.ShouldContain(s => s.Contains("linked: 010-demo depends on 020-auth"));
        report.Skipped.ShouldContain(s => s.Contains("linked: 010-demo depends on 030-storage"));
        report.Skipped.ShouldNotContain(s => s.Contains("unlinked"));

        using (var ctx = freshDb.CreateContext())
        {
            var blockers = await ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .SingleAsync(f => f.ProjectId == freshProjectId && f.Slug == "010-demo");
            var slugById = await ctx.PlanningFeatures
                .Where(f => f.ProjectId == freshProjectId)
                .ToDictionaryAsync(f => f.Id, f => f.Slug);

            blockers.BlockedBy
                .Select(link => slugById[link.BlockedByFeatureId])
                .OrderBy(slug => slug, StringComparer.Ordinal)
                .ShouldBe(["020-auth", "030-storage"]);
            (await ctx.PlanningFeatureDependencies.CountAsync()).ShouldBe(2);
        }

        using (var ctx = freshDb.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(freshProjectId, ct: CancellationToken.None);
        }

        (await SnapshotAsync()).ShouldBe(exported);
    }

    [Fact]
    public async Task Import_NonSortedDependsOn_RestoresFileOrderAndReExportIsStable()
    {
        var projectId = await SeedProjectAsync();
        WriteRaw("010-demo/feature.md",
            "---\n" +
            "feature: 010\n" +
            "slug: demo\n" +
            "status: ready\n" +
            "version: 1\n" +
            "created: 2026-09-01\n" +
            "depends_on: 030-storage, 020-auth\n" +
            "---\n\n" +
            "# Feature 010 — Demo feature title\n\n" +
            "<goal>\nBuild the demo.\n</goal>\n");
        WriteRaw("020-auth/feature.md",
            "---\n" +
            "feature: 020\n" +
            "slug: auth\n" +
            "status: done\n" +
            "version: 1\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 020 — Auth\n\n" +
            "<goal>\nAuth.\n</goal>\n");
        WriteRaw("030-storage/feature.md",
            "---\n" +
            "feature: 030\n" +
            "slug: storage\n" +
            "status: done\n" +
            "version: 1\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 030 — Storage\n\n" +
            "<goal>\nStorage.\n</goal>\n");

        var report = await ImportAsync(projectId);

        report.FeaturesCreated.ShouldBe(3);
        report.Skipped.ShouldNotContain(s => s.Contains("unlinked"));

        using (var ctx = _db.CreateContext())
        {
            var dependent = await ctx.PlanningFeatures
                .Include(f => f.BlockedBy)
                .SingleAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
            var slugById = await ctx.PlanningFeatures
                .Where(f => f.ProjectId == projectId)
                .ToDictionaryAsync(f => f.Id, f => f.Slug);

            dependent.BlockedBy
                .Select(link => slugById[link.BlockedByFeatureId])
                .OrderBy(slug => slug, StringComparer.Ordinal)
                .ShouldBe(["020-auth", "030-storage"]);
        }

        await SyncAsync(projectId, force: true);

        var demoDoc = await File.ReadAllTextAsync(Path.Combine(BacklogDir, "010-demo", "feature.md"));
        demoDoc.ShouldContain("depends_on: 020-auth, 030-storage\n");
        var exported = await SnapshotAsync();

        await SyncAsync(projectId);

        (await SnapshotAsync()).ShouldBe(exported);
    }

    [Fact]
    public async Task RoundTrip_TaskDependencyLinks_ExportThenImportIntoFreshDatabase_RestoresLinks()
    {
        var projectId = await SeedProjectAsync();
        WriteTaskDependencyFeature();

        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        var taskDir = Path.Combine(BacklogDir, "010-demo", "tasks");
        (await File.ReadAllTextAsync(Path.Combine(taskDir, "01-shell-task.md")))
            .ShouldNotContain("depends_on");
        (await File.ReadAllTextAsync(Path.Combine(taskDir, "02-pack-task.md")))
            .ShouldContain("depends_on: 01\n");
        (await File.ReadAllTextAsync(Path.Combine(taskDir, "03-ship-task.md")))
            .ShouldContain("depends_on: 01, 02\n");

        var exported = await SnapshotAsync();

        using var freshDb = new TempDatabase();
        Guid freshProjectId;
        using (var ctx = freshDb.CreateContext())
        {
            var project = Project.Create($"rt-fresh-{Guid.NewGuid():N}", _repoDir, string.Empty);
            project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            freshProjectId = project.Id;
        }

        BacklogImportReport report;
        using (var ctx = freshDb.CreateContext())
        {
            report = await CreateImporter(ctx)
                .ImportAsync(new BacklogImportRequest(_repoDir, freshProjectId), CancellationToken.None);
        }

        report.Skipped.Count(s => s.Contains("linked:") && s.Contains("tasks/")).ShouldBe(3);
        report.Skipped.ShouldNotContain(s => s.Contains("unlinked"));

        using (var ctx = freshDb.CreateContext())
        {
            (await ctx.PlanningTaskDependencies.CountAsync()).ShouldBe(3);
            var blocked = await ctx.PlanningArtifacts
                .Include(a => a.BlockedBy)
                .SingleAsync(a => a.Kind == PlanningArtifactKind.Task && a.Order == 3);
            var orders = await ctx.PlanningArtifacts.ToDictionaryAsync(a => a.Id, a => a.Order);
            blocked.BlockedBy
                .Select(d => orders[d.BlockingId])
                .OrderBy(o => o)
                .ShouldBe([1, 2]);
        }

        using (var ctx = freshDb.CreateContext())
        {
            await CreateMaterializer(ctx).SyncAsync(freshProjectId, ct: CancellationToken.None);
        }

        (await SnapshotAsync()).ShouldBe(exported);

        using (var ctx = freshDb.CreateContext())
        {
            var reimport = await CreateImporter(ctx)
                .ImportAsync(new BacklogImportRequest(_repoDir, freshProjectId), CancellationToken.None);
            reimport.ArtifactsCreated.ShouldBe(0);
            reimport.ArtifactsUpdated.ShouldBe(0);
            reimport.Skipped.ShouldNotContain(s => s.Contains("linked:"));
        }

        (await SnapshotAsync()).ShouldBe(exported);
    }

    private void WriteTaskDependencyFeature()
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
        WriteRaw("010-demo/tasks/01-shell-task.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            ---

            # Shell task

            <goal>
            Shell.
            </goal>
            """);
        WriteRaw("010-demo/tasks/02-pack-task.md",
            """
            ---
            feature: 010
            task: 02
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            depends_on: 01
            ---

            # Pack task

            <goal>
            Pack.
            </goal>
            """);
        WriteRaw("010-demo/tasks/03-ship-task.md",
            """
            ---
            feature: 010
            task: 03
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            depends_on: 01, 02
            ---

            # Ship task

            <goal>
            Ship.
            </goal>
            """);
    }

    [Fact]
    public async Task Import_UnknownPriority_DowngradesToP2WithReportLine()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature("priority: urgent");

        var report = await ImportAsync(projectId);

        report.Skipped.ShouldContain(
            s => s.Contains("unknown priority 'urgent'") && s.Contains("BACKLOG/010-demo/feature.md"));

        using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        feature.Priority.ShouldBe(FeaturePriority.P2);
    }

    [Fact]
    public async Task Import_MissingPriorityKey_OnNewFeature_DefaultsToP2Silently()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature(null);

        var report = await ImportAsync(projectId);

        report.Skipped.ShouldNotContain(s => s.Contains("priority"));

        using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        feature.Priority.ShouldBe(FeaturePriority.P2);
    }

    [Fact]
    public async Task ReImport_PriorityKeyRemovedFromDisk_ResetsBoardToP2LikeStatus()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature("priority: p1");
        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        WritePrioritizedFeature(null);
        var report = await ImportAsync(projectId);

        report.FeaturesUpdated.ShouldBe(1);
        report.Skipped.ShouldNotContain(s => s.Contains("priority"));

        using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
        feature.Priority.ShouldBe(FeaturePriority.P2);
    }

    [Fact]
    public async Task ReImport_HandEditedBrief_OnDisk_IsApplied()
    {
        var projectId = await SeedProjectAsync();
        WriteBriefedFeature();

        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        var taskPath = Path.Combine(BacklogDir, "010-demo", "tasks", "01-brief-task.md");
        var edited = (await File.ReadAllTextAsync(taskPath))
            .Replace("brief: Wire the polling hook", "brief: Edited on disk");
        await File.WriteAllTextAsync(taskPath, edited);

        var reimport = await ImportAsync(projectId);

        reimport.ArtifactsUpdated.ShouldBe(1);

        using var ctx = _db.CreateContext();
        var task = await ctx.PlanningArtifacts.SingleAsync(a => a.Kind == PlanningArtifactKind.Task);
        task.Brief.ShouldBe("Edited on disk");

        (await File.ReadAllTextAsync(taskPath)).ShouldContain("brief: Edited on disk\n");
    }

    [Fact]
    public async Task Import_FeatureWithDuplicatedHeadings_HealsToSingleHeading()
    {
        var projectId = await SeedProjectAsync();
        WriteRaw("010-demo/feature.md",
            "---\n" +
            "feature: 010\n" +
            "slug: demo\n" +
            "status: ready\n" +
            "version: 3\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 010 — Demo feature title\n\n" +
            "# Feature 010 — Demo feature title\n\n" +
            "# Feature 010 — Demo feature title\n\n" +
            "<goal>\nShip it.\n</goal>\n");

        await ImportAsync(projectId);

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.FirstAsync(f => f.ProjectId == projectId && f.Slug == "010-demo");
            feature.Title.ShouldBe("Demo feature title");
            feature.Body.ShouldBe("<goal>\nShip it.\n</goal>\n");
        }

        await SyncAsync(projectId, force: true);

        var healed = await File.ReadAllTextAsync(Path.Combine(BacklogDir, "010-demo", "feature.md"));
        CountHeadings(healed).ShouldBe(1);
        healed.ShouldContain("# Feature 010 — Demo feature title\n\n<goal>");

        var reimport = await ImportAsync(projectId);

        reimport.FeaturesCreated.ShouldBe(0);
        reimport.ArtifactsCreated.ShouldBe(0);
        reimport.ArtifactsUpdated.ShouldBe(0);
        (await File.ReadAllTextAsync(Path.Combine(BacklogDir, "010-demo", "feature.md"))).ShouldBe(healed);
    }

    [Fact]
    public async Task Import_CreatedFeaturesLandAtTheirPriorityBandTail()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature(null);
        WriteRaw("020-later/feature.md",
            "---\n" +
            "feature: 020\n" +
            "slug: later\n" +
            "status: ready\n" +
            "version: 1\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 020 — Later\n\n" +
            "<goal>\nLater.\n</goal>\n");
        WriteRaw("030-urgent/feature.md",
            "---\n" +
            "feature: 030\n" +
            "slug: urgent\n" +
            "status: ready\n" +
            "priority: p0\n" +
            "version: 1\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 030 — Urgent\n\n" +
            "<goal>\nUrgent.\n</goal>\n");

        var report = await ImportAsync(projectId);

        report.FeaturesCreated.ShouldBe(3);

        using var ctx = _db.CreateContext();
        var positions = await ctx.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .ToDictionaryAsync(f => f.Slug, f => f.QueuePosition);
        positions["010-demo"].ShouldBe(1);
        positions["020-later"].ShouldBe(2);
        positions["030-urgent"].ShouldBe(1);
    }

    [Fact]
    public async Task ReImport_LeavesQueuePositionUntouched_EvenWhenPriorityFlips()
    {
        var projectId = await SeedProjectAsync();
        WritePrioritizedFeature("priority: p1");
        await ImportAsync(projectId);
        await SyncAsync(projectId, force: true);

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(
                f => f.ProjectId == projectId && f.Slug == "010-demo");
            feature.QueuePosition.ShouldBe(1);
            await ctx.Database.ExecuteSqlAsync(
                $"UPDATE PlanningFeatures SET QueuePosition = 7 WHERE Id = {feature.Id}");
        }

        WritePrioritizedFeature(null);
        var report = await ImportAsync(projectId);

        report.FeaturesUpdated.ShouldBe(1);

        using (var ctx = _db.CreateContext())
        {
            var feature = await ctx.PlanningFeatures.SingleAsync(
                f => f.ProjectId == projectId && f.Slug == "010-demo");
            feature.Priority.ShouldBe(FeaturePriority.P2);
            feature.QueuePosition.ShouldBe(7);
        }
    }

    private async Task<Guid> SeedProjectAsync()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create($"rt-{Guid.NewGuid():N}", _repoDir, string.Empty);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private async Task<BacklogImportReport> ImportAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        return await CreateImporter(ctx)
            .ImportAsync(new BacklogImportRequest(_repoDir, projectId), CancellationToken.None);
    }

    private async Task SyncAsync(Guid projectId, bool force = false)
    {
        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, force: force, ct: CancellationToken.None);
    }

    private BacklogImporter CreateImporter(OvermindDbContext ctx) =>
        new(
            new PlanningFeatureService(
                ctx,
                CreateMaterializer(ctx),
                new BacklogImportSeeder(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx))),
                new TaskDependencyService(ctx, CreateMaterializer(ctx), NullLogger<TaskDependencyService>.Instance),
                NullLogger<PlanningFeatureService>.Instance),
            new FrontmatterKeyRegistry(ctx, NullLogger<FrontmatterKeyRegistry>.Instance));

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private async Task<Dictionary<string, string>> SnapshotAsync()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(BacklogDir, "*", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            snapshot[Path.GetRelativePath(_repoDir, file)] = await File.ReadAllTextAsync(file);
        }

        snapshot["BACKLOG.md"] = await File.ReadAllTextAsync(Path.Combine(_repoDir, "BACKLOG.md"));
        return snapshot;
    }

    private static int CountHeadings(string content) =>
        content.Split('\n').Count(line => line.StartsWith("# ", StringComparison.Ordinal));

    private void WriteRaw(string relativePath, string content)
    {
        var path = Path.Combine(BacklogDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteBriefedFeature()
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
        WriteRaw("010-demo/tasks/01-brief-task.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: todo
            review: light
            created: 2026-09-01
            brief: Wire the polling hook
            owner: bob
            ---

            # Brief task

            <goal>
            Do the thing.
            </goal>
            """);
    }

    private void WritePrioritizedFeature(string? priorityLine)
    {
        WriteRaw("010-demo/feature.md",
            "---\n" +
            "feature: 010\n" +
            "slug: demo\n" +
            "status: ready\n" +
            (priorityLine is null ? null : $"{priorityLine}\n") +
            "version: 3\n" +
            "created: 2026-09-01\n" +
            "---\n\n" +
            "# Feature 010 — Demo feature title\n\n" +
            "<goal>\nBuild the demo.\n</goal>\n");
    }

    private void WriteFullFeature()
    {
        WriteRaw("010-demo/feature.md",
            """
            ---
            feature: 010
            slug: demo
            status: ready
            version: 3
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
            status: validated
            created: 2026-09-01
            validated: 2026-09-02
            ---

            # Spec 010 — Demo feature title

            <scope>
            Spec body.
            </scope>

            <log>
            - 2026-09-01 — brainstormed.
            </log>
            """);
        WriteRaw("010-demo/plan.md",
            """
            ---
            feature: 010
            slug: demo
            status: draft
            created: 2026-09-01
            ---

            # Plan 010 — Demo feature title

            <approach>
            Plan body.
            </approach>
            """);
        WriteRaw("010-demo/result.md",
            """
            ---
            feature: 010
            slug: demo
            status: validated
            created: 2026-09-01
            validated: 2026-09-03
            ---

            # Result 010 — Demo feature title

            Ship summary.
            """);
        WriteRaw("010-demo/tasks/01-first-task.md",
            """
            ---
            feature: 010
            task: 01
            slug: demo
            status: done
            review: hard
            created: 2026-09-01
            ---

            # First task

            <goal>
            First task.
            </goal>

            <log>
            - 2026-09-01 — created from the plan.
            </log>
            """);
        WriteRaw("010-demo/tasks/02-second-task.md",
            """
            ---
            feature: 010
            task: 02
            slug: demo
            status: in_progress
            review: light
            created: 2026-09-01
            ---

            # Second task

            <goal>
            Second task.
            </goal>
            """);
    }
}
