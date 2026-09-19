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
