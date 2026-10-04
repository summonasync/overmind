using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningSectionMirrorRenderTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningSectionMirrorRenderTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-section-render-{Guid.NewGuid():N}");
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
    public async Task Sync_RepeatedObstaclePatchesCarryingWrappers_MirrorKeepsSingleWrapper()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        var taskId = await SeedTaskAsync(projectId);
        var taskPath = Path.Combine(_projectDir, "BACKLOG", "104-demo", "tasks", "01-patched-task.md");

        using (var ctx = _db.CreateContext())
        {
            var service = CreateArtifactService(ctx);
            await service.PatchAsync(taskId, new PlanningArtifactPatch(
                Section: "obstacles",
                Content: "<obstacles>\n" +
                    "- 2026-10-04 [agent] <round-1 B1, CLOSED by the fix round> first bullet\n" +
                    "</obstacles>"), CancellationToken.None);
        }

        var afterFirst = await File.ReadAllTextAsync(taskPath);
        CountTagLines(afterFirst, "<obstacles>").ShouldBe(1);
        CountTagLines(afterFirst, "</obstacles>").ShouldBe(1);
        StripFrontmatter(afterFirst).ShouldNotBeNull();

        using (var ctx = _db.CreateContext())
        {
            var service = CreateArtifactService(ctx);
            await service.PatchAsync(taskId, new PlanningArtifactPatch(
                Section: "obstacles",
                Content: "<obstacles>\n" +
                    "- 2026-10-04 [agent] <round-1 B1, CLOSED by the fix round> first bullet\n" +
                    "- 2026-10-04 [agent] <round-2 W-1, CLOSED by round 3> second bullet\n" +
                    "</obstacles>"), CancellationToken.None);
        }

        var afterSecond = await File.ReadAllTextAsync(taskPath);
        CountTagLines(afterSecond, "<obstacles>").ShouldBe(1);
        CountTagLines(afterSecond, "</obstacles>").ShouldBe(1);

        var body = StripFrontmatter(afterSecond);
        var obstacles = PlanningSections.Find("obstacles", body);
        obstacles.ShouldNotBeNull();
        obstacles!.Content.ShouldContain("first bullet");
        obstacles.Content.ShouldContain("second bullet");
        obstacles.Content.ShouldContain("<round-1 B1, CLOSED by the fix round>");
        afterSecond.IndexOf("second bullet", StringComparison.Ordinal)
            .ShouldBeLessThan(afterSecond.IndexOf("</obstacles>", StringComparison.Ordinal));

        var tail = afterSecond[(afterSecond.IndexOf("</obstacles>", StringComparison.Ordinal) + "</obstacles>".Length)..];
        tail.TrimStart().ShouldStartWith("<log>");
    }

    [Fact]
    public async Task Sync_CorruptedBoardBody_RegeneratesSingleWrapperMirror()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedTaskAsync(
            projectId,
            "# Corrupted Task\n\n" +
            "<goal>\nDo it.\n</goal>\n\n" +
            "<obstacles>\n" +
            "<obstacles>\n" +
            "- kept bullet <round-1 B1, CLOSED by the fix round>\n" +
            "</obstacles>\n" +
            "- evicted addendum bullet from an earlier patch\n" +
            "</obstacles>\n\n" +
            "<log>\n- 2026-10-04 [agent] seeded\n</log>\n");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "104-demo", "tasks", "01-corrupted-task.md");
        var rendered = await File.ReadAllTextAsync(taskPath);
        CountTagLines(rendered, "<obstacles>").ShouldBe(1);
        CountTagLines(rendered, "</obstacles>").ShouldBe(1);
        rendered.ShouldContain("- kept bullet <round-1 B1, CLOSED by the fix round>");
        rendered.ShouldNotContain("evicted addendum bullet");
        rendered.ShouldContain("- 2026-10-04 [agent] seeded");
        CountTagLines(rendered, "<log>").ShouldBe(1);
        CountTagLines(rendered, "</log>").ShouldBe(1);

        var stable = await File.ReadAllBytesAsync(taskPath);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        (await File.ReadAllBytesAsync(taskPath)).ShouldBe(stable);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_StrayCloseAfterLogSection_LogSurvivesAndStrayLeaves()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedTaskAsync(
            projectId,
            "# Stray Tail\n\n" +
            "<obstacles>\n- only bullet\n</obstacles>\n\n" +
            "<log>\n- 2026-10-04 [agent] seeded\n</log>\n" +
            "</obstacles>\n");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "104-demo", "tasks", "01-stray-tail.md");
        var rendered = await File.ReadAllTextAsync(taskPath);
        CountTagLines(rendered, "<obstacles>").ShouldBe(1);
        CountTagLines(rendered, "</obstacles>").ShouldBe(1);
        CountTagLines(rendered, "<log>").ShouldBe(1);
        CountTagLines(rendered, "</log>").ShouldBe(1);
        rendered.ShouldContain("- only bullet");
        rendered.ShouldContain("- 2026-10-04 [agent] seeded");
        rendered.IndexOf("- 2026-10-04 [agent] seeded", StringComparison.Ordinal)
            .ShouldBeGreaterThan(rendered.IndexOf("- only bullet", StringComparison.Ordinal));
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

    private async Task<Guid> SeedTaskAsync(Guid projectId, string? body = null)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "104-demo", "Section Render", "<goal>\nShip it.\n</goal>");
        feature.SetStatus(PlanningFeatureStatus.InProgress);
        ctx.PlanningFeatures.Add(feature);

        var task = PlanningArtifact.Create(
            feature.Id,
            PlanningArtifactKind.Task,
            body ?? "# Patched Task\n\n<obstacles>\n</obstacles>\n\n<log>\n- 2026-10-04 [agent] seeded\n</log>\n",
            1);
        ctx.PlanningArtifacts.Add(task);
        await ctx.SaveChangesAsync();
        return task.Id;
    }

    private PlanningArtifactService CreateArtifactService(OvermindDbContext ctx) =>
        new(
            ctx,
            CreateMaterializer(ctx),
            NullLogger<PlanningArtifactService>.Instance,
            new TaskDependencyService(ctx, CreateMaterializer(ctx), NullLogger<TaskDependencyService>.Instance));

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private static int CountTagLines(string content, string tag) =>
        content.Split('\n').Count(line => line.Trim(' ', '\t').TrimEnd('\r') == tag);

    private static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
        {
            return content;
        }

        var boundary = content.IndexOf("\n---\n", StringComparison.Ordinal);
        return boundary < 0 ? content : content[(boundary + 5)..];
    }
}
