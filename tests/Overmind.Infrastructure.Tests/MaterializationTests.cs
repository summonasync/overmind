using System.Text;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class MaterializationTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    public MaterializationTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-mat-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
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
    public async Task Materialize_WritesExactFilesAndTracksRows()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);

        var claudeReviewer = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var kimiReviewer = Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md");
        var claudeTester = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        var claudeBootstrap = Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md");
        var kimiBootstrap = Path.Combine(_projectDir, ".kimi-code", "skills", "overmind-bootstrap", "SKILL.md");

        File.Exists(claudeReviewer).ShouldBeTrue();
        File.Exists(kimiReviewer).ShouldBeTrue();
        File.Exists(claudeTester).ShouldBeTrue();
        File.Exists(claudeBootstrap).ShouldBeTrue();
        File.Exists(kimiBootstrap).ShouldBeTrue();

        var expectedReviewer = "# Reviewer\nRun on every change.\n";
        var bytes = await File.ReadAllBytesAsync(claudeReviewer);
        bytes[0].ShouldNotBe((byte)0xEF);
        Encoding.UTF8.GetString(bytes).ShouldBe(expectedReviewer);

        var expectedTester = "# Tester\nExec suite.\n";
        (await File.ReadAllTextAsync(claudeTester)).ShouldBe(expectedTester);

        report.Files.Count.ShouldBe(5);

        var rows = await ctx.MaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Count.ShouldBe(5);
        rows.Select(r => r.RelativePath).OrderBy(p => p, StringComparer.Ordinal).ShouldBe(
            new[]
            {
                ".claude/skills/overmind-bootstrap/SKILL.md",
                ".claude/skills/reviewer/reviewer/SKILL.md",
                ".claude/skills/tester/tester/SKILL.md",
                ".kimi-code/skills/overmind-bootstrap/SKILL.md",
                ".kimi-code/skills/reviewer/reviewer/SKILL.md",
            });

        foreach (var row in rows)
        {
            var diskBytes = await File.ReadAllBytesAsync(Path.Combine(_projectDir, row.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(diskBytes));
            hash.ShouldBe(row.Sha256);
        }
    }

    [Fact]
    public async Task Materialize_BootstrapContainsGateGuardContentAndCtxLinks()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);

        var bootstrap = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md"));

        bootstrap.ShouldContain($"ctx://projects/{projectId}/gates");
        bootstrap.ShouldContain($"ctx://projects/{projectId}/guards");
        bootstrap.ShouldContain("All tests must pass before merge.");
        bootstrap.ShouldContain("Never force push to main.");
        bootstrap.ShouldContain("### Tests Green");
        bootstrap.ShouldContain("### No Force Push");
    }

    [Fact]
    public async Task Dematerialize_RemovesOnlyTrackedFiles()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        var foreign = Path.Combine(_projectDir, ".claude", "skills", "foreign", "KEEP.md");
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(foreign, "user file");
        var notes = Path.Combine(_projectDir, "notes.txt");
        await File.WriteAllTextAsync(notes, "personal notes");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);
        var report = await materializer.DematerializeAsync(projectId, kitId, CancellationToken.None);

        report.Files.Count.ShouldBe(5);

        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "overmind-bootstrap", "SKILL.md")).ShouldBeFalse();

        File.Exists(foreign).ShouldBeTrue();
        File.Exists(notes).ShouldBeTrue();

        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".kimi-code")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "foreign")).ShouldBeTrue();
    }

    [Fact]
    public async Task SwitchKits_LeavesOnlySecondKitFiles()
    {
        Guid projectId;
        Guid kitAId;
        Guid kitBId;
        using (var ctx = _db.CreateContext())
        {
            var project = Project.Create("demo", _projectDir);
            ctx.Projects.Add(project);

            var reviewer = ContextEntry.CreateSkill(
                project.Id,
                "Reviewer",
                "Reviews code",
                [SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer\n")]);
            var tester = ContextEntry.CreateSkill(
                project.Id,
                "Tester",
                "Runs tests",
                [SkillFile.Create(AgentPlatform.Claude, "tester/SKILL.md", "# Tester\n")]);
            var gate = ContextEntry.Create(project.Id, EntryType.Gate, "Tests Green", "Gate from kit A.");
            var guard = ContextEntry.Create(project.Id, EntryType.Guard, "No Force Push", "Guard from kit B.");

            var kitA = SkillKit.Create("pack-a", 1, "A");
            kitA.AddEntry(reviewer.Id);
            kitA.AddEntry(gate.Id);
            var kitB = SkillKit.Create("pack-b", 1, "B");
            kitB.AddEntry(tester.Id);
            kitB.AddEntry(guard.Id);

            ctx.ContextEntries.AddRange(reviewer, tester, gate, guard);
            ctx.SkillKits.AddRange(kitA, kitB);
            await ctx.SaveChangesAsync();

            projectId = project.Id;
            kitAId = kitA.Id;
            kitBId = kitB.Id;
        }

        using var ctx2 = _db.CreateContext();
        var materializer = CreateMaterializer(ctx2);

        await materializer.MaterializeAsync(projectId, kitAId, CancellationToken.None);

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        File.Exists(reviewerFile).ShouldBeTrue();
        File.Exists(testerFile).ShouldBeFalse();

        await materializer.DematerializeAsync(projectId, kitAId, CancellationToken.None);
        await materializer.MaterializeAsync(projectId, kitBId, CancellationToken.None);

        File.Exists(reviewerFile).ShouldBeFalse();
        File.Exists(testerFile).ShouldBeTrue();

        var bootstrap = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md"));
        bootstrap.ShouldContain("Guard from kit B.");
        bootstrap.ShouldNotContain("Gate from kit A.");
    }

    [Fact]
    public async Task DetectDrift_ReportsModifiedAndMissingFiles()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);

        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        var kimiReviewer = Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md");

        await File.AppendAllTextAsync(testerFile, "external edit\n");
        File.Delete(kimiReviewer);

        var items = await materializer.DetectDriftAsync(projectId, CancellationToken.None);

        items.Count.ShouldBe(2);
        items.ShouldContain(i =>
            i.Kind == DriftKind.Modified &&
            i.RelativePath == ".claude/skills/tester/tester/SKILL.md" &&
            i.KitId == kitId);
        items.ShouldContain(i =>
            i.Kind == DriftKind.Missing &&
            i.RelativePath == ".kimi-code/skills/reviewer/reviewer/SKILL.md" &&
            i.Platform == AgentPlatform.KimiCode);
    }

    [Fact]
    public async Task Materialize_WithUntrackedFileAtTargetPath_FailsAndNamesPath()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        var target = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "user data");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.OverwriteBlocked);
        ex.Message.ShouldContain(target);
        ex.OffendingPath.ShouldBe(target);

        (await File.ReadAllTextAsync(target)).ShouldBe("user data");

        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Materialize_ReMaterializingOwnFiles_Succeeds()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);
        var again = await materializer.MaterializeAsync(projectId, kitId, CancellationToken.None);

        again.Files.Count.ShouldBe(5);
        (await ctx.MaterializedFiles.Where(m => m.ProjectId == projectId).CountAsync()).ShouldBe(5);

        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        (await File.ReadAllTextAsync(testerFile)).ShouldBe("# Tester\nExec suite.\n");
    }

    [Fact]
    public async Task Materialize_WithTraversalRelativePath_ThrowsAndWritesNothing()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync(maliciousTesterPath: "../../evil.md");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.PathTraversal);
        ex.Message.ShouldContain("../../evil.md");

        File.Exists(Path.Combine(_sandboxDir, "evil.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_sandboxDir, "project", "evil.md")).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Materialize_WithAbsoluteRelativePath_ThrowsPathTraversal()
    {
        var absolute = Path.Combine(_sandboxDir, "escaped.md");
        var (projectId, kitId) = await SeedDefaultKitAsync(maliciousTesterPath: absolute);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.PathTraversal);
        File.Exists(absolute).ShouldBeFalse();
    }

    [Fact]
    public async Task Materialize_WithDirectoryAtFirstTargetPath_ReportsOverwriteBlocked()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        var directoryAtTarget = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(directoryAtTarget);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.OverwriteBlocked);
        ex.Message.ShouldContain(directoryAtTarget);

        Directory.Exists(directoryAtTarget).ShouldBeTrue();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Materialize_WhenDirectoryCreationFailsMidWrite_RollsBackAndReportsIoError()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");

        var testerSlugDir = Path.Combine(_projectDir, ".claude", "skills", "tester");
        Directory.CreateDirectory(testerSlugDir);
        await File.WriteAllTextAsync(Path.Combine(testerSlugDir, "tester"), "a file where a directory is expected");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.IoError);
        ex.OffendingPath.ShouldBe(testerFile);
        ex.Message.ShouldContain(testerFile);

        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);

        File.Exists(reviewerFile).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(testerSlugDir, "tester")).ShouldBeTrue();
    }

    private async Task<(Guid ProjectId, Guid KitId)> SeedDefaultKitAsync(string? maliciousTesterPath = null)
    {
        using var ctx = _db.CreateContext();

        var project = Project.Create("demo", _projectDir);
        ctx.Projects.Add(project);

        var reviewer = ContextEntry.CreateSkill(
            project.Id,
            "Reviewer",
            "Reviews code",
            new[]
            {
                SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer\r\nRun on every change.\r\n"),
                SkillFile.Create(AgentPlatform.KimiCode, "reviewer/SKILL.md", "# Reviewer\r\nRun on every change.\r\n"),
            });

        var tester = ContextEntry.CreateSkill(
            project.Id,
            "Tester",
            "Runs tests",
            [SkillFile.Create(AgentPlatform.Claude, maliciousTesterPath ?? "tester/SKILL.md", "# Tester\r\nExec suite.\r\n")]);

        var gate = ContextEntry.Create(project.Id, EntryType.Gate, "Tests Green", "All tests must pass before merge.");
        var guard = ContextEntry.Create(project.Id, EntryType.Guard, "No Force Push", "Never force push to main.");

        var kit = SkillKit.Create("quality-pack", 1, "quality gates");
        kit.AddEntry(reviewer.Id);
        kit.AddEntry(tester.Id);
        kit.AddEntry(gate.Id);
        kit.AddEntry(guard.Id);

        ctx.ContextEntries.AddRange(reviewer, tester, gate, guard);
        ctx.SkillKits.Add(kit);
        await ctx.SaveChangesAsync();

        return (project.Id, kit.Id);
    }

    private static SkillMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, PlatformDirectoryMap.Load());
}
