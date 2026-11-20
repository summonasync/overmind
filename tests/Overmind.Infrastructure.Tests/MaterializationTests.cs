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
        var report = await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var claudeReviewer = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var kimiReviewer = Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md");
        var claudeTester = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        var claudeBootstrap = Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack", "SKILL.md");
        var kimiBootstrap = Path.Combine(_projectDir, ".kimi-code", "skills", "overmind-bootstrap-quality-pack", "SKILL.md");

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
                ".claude/skills/overmind-bootstrap-quality-pack/SKILL.md",
                ".claude/skills/reviewer/reviewer/SKILL.md",
                ".claude/skills/tester/tester/SKILL.md",
                ".kimi-code/skills/overmind-bootstrap-quality-pack/SKILL.md",
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
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var bootstrap = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack", "SKILL.md"));

        bootstrap.ShouldContain("# Overmind Bootstrap — quality-pack");
        bootstrap.ShouldContain($"overmind://projects/{projectId}/gates");
        bootstrap.ShouldContain($"overmind://projects/{projectId}/guards");
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
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);
        var report = await materializer.DematerializeAsync(projectId, kitId, CancellationToken.None);

        report.Files.Count.ShouldBe(5);

        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "overmind-bootstrap-quality-pack", "SKILL.md")).ShouldBeFalse();

        File.Exists(foreign).ShouldBeTrue();
        File.Exists(notes).ShouldBeTrue();

        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack")).ShouldBeFalse();
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

        await materializer.MaterializeAsync(projectId, kitAId, ct: CancellationToken.None);

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        File.Exists(reviewerFile).ShouldBeTrue();
        File.Exists(testerFile).ShouldBeFalse();

        await materializer.DematerializeAsync(projectId, kitAId, CancellationToken.None);
        await materializer.MaterializeAsync(projectId, kitBId, ct: CancellationToken.None);

        File.Exists(reviewerFile).ShouldBeFalse();
        File.Exists(testerFile).ShouldBeTrue();

        var bootstrap = await File.ReadAllTextAsync(
            Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-pack-b", "SKILL.md"));
        bootstrap.ShouldContain("Guard from kit B.");
        bootstrap.ShouldNotContain("Gate from kit A.");
    }

    [Fact]
    public async Task DetectDrift_ReportsModifiedAndMissingFiles()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

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
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.OverwriteBlocked);
        ex.Message.ShouldContain(target);
        ex.OffendingPath.ShouldBe(target);

        (await File.ReadAllTextAsync(target)).ShouldBe("user data");

        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Materialize_WithUntrackedFileAtTargetPath_ForceAdoptsFileAndWarns()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        var target = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "user data");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var report = await materializer.MaterializeAsync(projectId, kitId, force: true, ct: CancellationToken.None);

        report.Files.Count.ShouldBe(5);
        report.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote untracked file: {target}");
        (await File.ReadAllTextAsync(target)).ShouldBe("# Reviewer\nRun on every change.\n");
        (await ctx.MaterializedFiles.Where(m => m.ProjectId == projectId).CountAsync()).ShouldBe(5);
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Materialize_ReMaterializingOwnFiles_Succeeds()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);
        var again = await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        again.Files.Count.ShouldBe(5);
        again.Warnings.ShouldBeEmpty();
        (await ctx.MaterializedFiles.Where(m => m.ProjectId == projectId).CountAsync()).ShouldBe(5);

        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        (await File.ReadAllTextAsync(testerFile)).ShouldBe("# Tester\nExec suite.\n");
    }

    [Fact]
    public async Task Materialize_TrackedSkillFilesHandEdited_RefusesOnceListingEveryPath()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var testerFile = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        await File.AppendAllTextAsync(reviewerFile, "\nhand note\n");
        await File.AppendAllTextAsync(testerFile, "\nhand edit\n");

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.LocalEditsBlocked);
        ex.Message.ShouldContain(reviewerFile);
        ex.Message.ShouldContain(testerFile);
        ex.Message.ShouldContain("re-run materialize with force");
        (await File.ReadAllTextAsync(reviewerFile)).ShouldContain("hand note");
        (await File.ReadAllTextAsync(testerFile)).ShouldContain("hand edit");

        var drift = await materializer.DetectDriftAsync(projectId, CancellationToken.None);
        drift.Count.ShouldBe(2);
        drift.ShouldAllBe(d => d.Kind == DriftKind.Modified);
    }

    [Fact]
    public async Task Materialize_TrackedSkillFileHandEdited_ForceOverwritesAndWarnsPerFile()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var reviewerFile = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        await File.AppendAllTextAsync(reviewerFile, "\nhand note\n");

        var report = await materializer.MaterializeAsync(projectId, kitId, force: true, ct: CancellationToken.None);

        report.Warnings.ShouldHaveSingleItem().ShouldBe($"Overwrote locally modified file: {reviewerFile}");
        (await File.ReadAllTextAsync(reviewerFile)).ShouldBe("# Reviewer\nRun on every change.\n");
        (await materializer.DetectDriftAsync(projectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Materialize_WithTraversalRelativePath_ThrowsAndWritesNothing()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync(maliciousTesterPath: "../../evil.md");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

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
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

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
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

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
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.IoError);
        ex.OffendingPath.ShouldBe(testerFile);
        ex.Message.ShouldContain(testerFile);

        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);

        File.Exists(reviewerFile).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(testerSlugDir, "tester")).ShouldBeTrue();
    }

    [Fact]
    public async Task Materialize_AgnosticSkillExpandsToEveryPlatformMarkedSkillStaysExclusive()
    {
        var (projectId, kitId) = await SeedMixedKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var report = await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var platformDirs = new[]
        {
            ".claude", ".kimi-code", ".agents", ".gemini", ".antigravity", ".qwen"
        };

        foreach (var dir in platformDirs)
        {
            File.Exists(Path.Combine(_projectDir, dir, "skills", "linter", "linter", "SKILL.md")).ShouldBeTrue();
            File.Exists(Path.Combine(_projectDir, dir, "skills", "overmind-bootstrap-mixed-pack", "SKILL.md")).ShouldBeTrue();
        }

        var claudeLinter = await File.ReadAllBytesAsync(
            Path.Combine(_projectDir, ".claude", "skills", "linter", "linter", "SKILL.md"));
        var qwenLinter = await File.ReadAllBytesAsync(
            Path.Combine(_projectDir, ".qwen", "skills", "linter", "linter", "SKILL.md"));
        qwenLinter.ShouldBe(claudeLinter);

        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_projectDir, ".kimi-code", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();
        File.Exists(Path.Combine(_projectDir, ".qwen", "skills", "reviewer", "reviewer", "SKILL.md")).ShouldBeFalse();

        var expectedContent = "# Linter\nChecks style.\n";
        (await File.ReadAllTextAsync(Path.Combine(_projectDir, ".gemini", "skills", "linter", "linter", "SKILL.md")))
            .ShouldBe(expectedContent);

        report.Files.Count.ShouldBe(13);

        var rows = await ctx.MaterializedFiles.Where(m => m.ProjectId == projectId).ToListAsync();
        rows.Count.ShouldBe(13);
        rows.Count(r => r.Platform == AgentPlatform.Qwen).ShouldBe(2);
        rows.Count(r => r.RelativePath == ".qwen/skills/linter/linter/SKILL.md").ShouldBe(1);
        rows.ShouldContain(r =>
            r.Platform == AgentPlatform.Qwen &&
            r.RelativePath == ".qwen/skills/overmind-bootstrap-mixed-pack/SKILL.md");
        rows.ShouldContain(r =>
            r.Platform == AgentPlatform.Claude &&
            r.RelativePath == ".claude/skills/reviewer/reviewer/SKILL.md");
        rows.ShouldAllBe(r => r.Sha256.Length == 64);
    }

    [Fact]
    public async Task DetectDrift_ReportsEditsInExpandedAgnosticFiles()
    {
        var (projectId, kitId) = await SeedMixedKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var qwenLinter = Path.Combine(_projectDir, ".qwen", "skills", "linter", "linter", "SKILL.md");
        var geminiLinter = Path.Combine(_projectDir, ".gemini", "skills", "linter", "linter", "SKILL.md");

        await File.AppendAllTextAsync(qwenLinter, "local edit\n");
        File.Delete(geminiLinter);

        var items = await materializer.DetectDriftAsync(projectId, CancellationToken.None);

        items.Count.ShouldBe(2);
        items.ShouldContain(i =>
            i.Kind == DriftKind.Modified &&
            i.RelativePath == ".qwen/skills/linter/linter/SKILL.md" &&
            i.Platform == AgentPlatform.Qwen &&
            i.KitId == kitId);
        items.ShouldContain(i =>
            i.Kind == DriftKind.Missing &&
            i.RelativePath == ".gemini/skills/linter/linter/SKILL.md" &&
            i.Platform == AgentPlatform.Gemini);
    }

    [Fact]
    public async Task Materialize_TrackedRowFromPreviousBootstrapLayout_RemovesStaleFileOnlyForThatKit()
    {
        var (projectId, kitId) = await SeedDefaultKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        var staleRow = MaterializedFile.Create(
            projectId,
            kitId,
            AgentPlatform.Claude,
            ".claude/skills/overmind-bootstrap/SKILL.md",
            ContentHash.Sha256("# stale layout bootstrap\n"));
        var foreignKit = SkillKit.Create("foreign-pack", 1, "kept");
        ctx.SkillKits.Add(foreignKit);
        var foreignRow = MaterializedFile.Create(
            projectId,
            foreignKit.Id,
            AgentPlatform.Claude,
            ".claude/skills/foreign/KEEP.md",
            ContentHash.Sha256("foreign kit file\n"));
        ctx.MaterializedFiles.AddRange(staleRow, foreignRow);
        await ctx.SaveChangesAsync();

        var stalePath = Path.Combine(_projectDir, ".claude", "skills", "overmind-bootstrap", "SKILL.md");
        var foreignPath = Path.Combine(_projectDir, ".claude", "skills", "foreign", "KEEP.md");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(foreignPath)!);
        await File.WriteAllTextAsync(stalePath, "# stale layout bootstrap\n");
        await File.WriteAllTextAsync(foreignPath, "foreign kit file\n");

        var report = await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);

        File.Exists(stalePath).ShouldBeFalse();
        report.Warnings.ShouldContain(w => w.Contains("overmind-bootstrap/SKILL.md", StringComparison.Ordinal));
        File.Exists(foreignPath).ShouldBeTrue();
        (await ctx.MaterializedFiles
            .AnyAsync(m => m.RelativePath == ".claude/skills/overmind-bootstrap/SKILL.md")).ShouldBe(false);
        (await ctx.MaterializedFiles.AnyAsync(m => m.KitId == foreignKit.Id)).ShouldBeTrue();
        File.Exists(Path.Combine(
            _projectDir, ".claude", "skills", "overmind-bootstrap-quality-pack", "SKILL.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task Dematerialize_RemovesAllExpandedAgnosticFiles()
    {
        var (projectId, kitId) = await SeedMixedKitAsync();

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None);
        var report = await materializer.DematerializeAsync(projectId, kitId, CancellationToken.None);

        report.Files.Count.ShouldBe(13);
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);

        foreach (var dir in new[] { ".claude", ".kimi-code", ".agents", ".gemini", ".antigravity", ".qwen" })
        {
            Directory.Exists(Path.Combine(_projectDir, dir)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Materialize_WithUntrackedFileAtExpandedTargetPath_BlocksOverwrite()
    {
        var (projectId, kitId) = await SeedMixedKitAsync();

        var target = Path.Combine(_projectDir, ".qwen", "skills", "linter", "linter", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "user data");

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(projectId, kitId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.OverwriteBlocked);
        ex.OffendingPath.ShouldBe(target);

        (await File.ReadAllTextAsync(target)).ShouldBe("user data");
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "linter", "linter", "SKILL.md")).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Materialize_SkillMixingAgnosticAndExplicitFileSameRelativePath_ThrowsDuplicateTargetAndWritesNothing()
    {
        using var ctx = _db.CreateContext();

        var project = Project.Create("demo", _projectDir);
        ctx.Projects.Add(project);

        var hybrid = ContextEntry.CreateSkill(
            project.Id,
            "hybrid",
            "mixed origin",
            new[]
            {
                SkillFile.Create(null, "hybrid/SKILL.md", "# Agnostic copy\n"),
                SkillFile.Create(AgentPlatform.Claude, "hybrid/SKILL.md", "# Explicit copy\n"),
            });

        var kit = SkillKit.Create("hybrid-pack", 1, "mixed");
        kit.AddEntry(hybrid.Id);

        ctx.ContextEntries.Add(hybrid);
        ctx.SkillKits.Add(kit);
        await ctx.SaveChangesAsync();

        var materializer = CreateMaterializer(ctx);
        var target = Path.Combine(_projectDir, ".claude", "skills", "hybrid", "hybrid", "SKILL.md");

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.MaterializeAsync(project.Id, kit.Id, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.InvalidTarget);
        ex.OffendingPath.ShouldBe(target);
        ex.Message.ShouldContain(target);

        File.Exists(target).ShouldBeFalse();
        Directory.Exists(Path.Combine(_projectDir, ".claude")).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync()).ShouldBe(0);
    }

    private async Task<(Guid ProjectId, Guid KitId)> SeedMixedKitAsync()
    {
        using var ctx = _db.CreateContext();

        var project = Project.Create("demo", _projectDir);
        ctx.Projects.Add(project);

        var linter = ContextEntry.CreateSkill(
            project.Id,
            "Linter",
            "Checks style",
            [SkillFile.Create(null, "linter/SKILL.md", "# Linter\nChecks style.\n")]);

        var reviewer = ContextEntry.CreateSkill(
            project.Id,
            "Reviewer",
            "Reviews code",
            [SkillFile.Create(AgentPlatform.Claude, "reviewer/SKILL.md", "# Reviewer\n")]);

        var gate = ContextEntry.Create(project.Id, EntryType.Gate, "Tests Green", "All tests must pass before merge.");

        var kit = SkillKit.Create("mixed-pack", 1, "mixed");
        kit.AddEntry(linter.Id);
        kit.AddEntry(reviewer.Id);
        kit.AddEntry(gate.Id);

        ctx.ContextEntries.AddRange(linter, reviewer, gate);
        ctx.SkillKits.Add(kit);
        await ctx.SaveChangesAsync();

        return (project.Id, kit.Id);
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
