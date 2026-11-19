using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class KitActivationServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _projectDir;

    public KitActivationServiceTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"overmind-activate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }

    [Fact]
    public async Task Activate_SecondOptionalKit_StacksBothKitsFiles()
    {
        using var ctx = _db.CreateContext();
        var (projectId, kitAId, kitBId) = await SeedTwoOptionalKitsAsync(ctx);
        var service = CreateService(ctx);

        var first = await service.ActivateAsync(projectId, kitAId, false, CancellationToken.None);
        var second = await service.ActivateAsync(projectId, kitBId, false, CancellationToken.None);

        first.Files.Count.ShouldBe(2);
        second.Files.Count.ShouldBe(2);

        var reviewer = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var tester = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        File.Exists(reviewer).ShouldBeTrue();
        File.Exists(tester).ShouldBeTrue();

        var bootstraps = second.Files
            .Concat(first.Files)
            .Where(f => f.RelativePath.EndsWith("SKILL.md") && f.RelativePath.Contains("overmind-bootstrap"))
            .Select(f => f.RelativePath)
            .Distinct()
            .ToArray();
        bootstraps.Length.ShouldBe(2);

        var activeLinks = await ctx.ProjectKits
            .Where(pk => pk.ProjectId == projectId && pk.IsActive)
            .ToListAsync();
        activeLinks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Activate_MandatoryGlobalKit_ThrowsAndWritesNothing()
    {
        using var ctx = _db.CreateContext();
        var (projectId, kitAId, mandatoryKitId) = await SeedTwoOptionalKitsAsync(ctx);
        var mandatoryKit = await ctx.SkillKits.FirstAsync(k => k.Id == mandatoryKitId, CancellationToken.None);
        mandatoryKit.SetMandatory(true);
        await ctx.SaveChangesAsync();
        var service = CreateService(ctx);

        var failure = await Should.ThrowAsync<KitActivationException>(
            () => service.ActivateAsync(projectId, mandatoryKitId, false, CancellationToken.None));

        failure.Kind.ShouldBe(KitActivationFailureKind.MandatoryGlobal);
        failure.Message.ShouldContain("mandatory-global");
        await ctx.ProjectKits.CountAsync(pk => pk.KitId == mandatoryKitId).ShouldBe(0);
        ctx.MaterializedFiles.Count(m => m.KitId == mandatoryKitId).ShouldBe(0);
        Directory.Exists(_projectDir).ShouldBeTrue();
        Directory.EnumerateFiles(_projectDir).ShouldBeEmpty();

        await service.ActivateAsync(projectId, kitAId, false, CancellationToken.None);
        File.Exists(Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md"))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivate_OneStackedKit_KeepsTheOtherKitFiles()
    {
        using var ctx = _db.CreateContext();
        var (projectId, kitAId, kitBId) = await SeedTwoOptionalKitsAsync(ctx);
        var service = CreateService(ctx);
        await service.ActivateAsync(projectId, kitAId, false, CancellationToken.None);
        await service.ActivateAsync(projectId, kitBId, false, CancellationToken.None);

        await service.DeactivateAsync(projectId, kitBId, CancellationToken.None);

        var reviewer = Path.Combine(_projectDir, ".claude", "skills", "reviewer", "reviewer", "SKILL.md");
        var tester = Path.Combine(_projectDir, ".claude", "skills", "tester", "tester", "SKILL.md");
        var bootstrapB = Path.Combine(
            _projectDir, ".claude", "skills", "overmind-bootstrap-test-pack", "SKILL.md");
        var bootstrapA = Path.Combine(
            _projectDir, ".claude", "skills", "overmind-bootstrap-review-pack", "SKILL.md");
        File.Exists(reviewer).ShouldBeTrue();
        File.Exists(bootstrapA).ShouldBeTrue();
        File.Exists(tester).ShouldBeFalse();
        File.Exists(bootstrapB).ShouldBeFalse();
        (await ctx.MaterializedFiles.CountAsync(m => m.KitId == kitBId)).ShouldBe(0);
    }

    [Fact]
    public async Task Deactivate_UnlinkedKit_ThrowsNotLinked()
    {
        using var ctx = _db.CreateContext();
        var (projectId, _, kitBId) = await SeedTwoOptionalKitsAsync(ctx);
        var service = CreateService(ctx);

        var failure = await Should.ThrowAsync<KitActivationException>(
            () => service.DeactivateAsync(projectId, kitBId, CancellationToken.None));

        failure.Kind.ShouldBe(KitActivationFailureKind.NotLinked);
    }

    [Fact]
    public async Task Activate_UnknownKit_ThrowsMissingKit()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        var service = CreateService(ctx);

        var failure = await Should.ThrowAsync<MaterializationException>(
            () => service.ActivateAsync(project.Id, Guid.NewGuid(), false, CancellationToken.None));

        failure.Kind.ShouldBe(MaterializationFailureKind.NotFound);
        await ctx.ProjectKits.AnyAsync().ShouldBe(false);
    }

    private async Task<(Guid ProjectId, Guid KitAId, Guid KitBId)> SeedTwoOptionalKitsAsync(OvermindDbContext ctx)
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
        var gate = ContextEntry.Create(project.Id, EntryType.Gate, "Tests Green", "Gate body.");
        var guard = ContextEntry.Create(project.Id, EntryType.Guard, "No Force Push", "Guard body.");

        var kitA = SkillKit.Create("review-pack", 1, "A");
        kitA.AddEntry(reviewer.Id);
        kitA.AddEntry(gate.Id);
        var kitB = SkillKit.Create("test-pack", 1, "B");
        kitB.AddEntry(tester.Id);
        kitB.AddEntry(guard.Id);

        ctx.ContextEntries.AddRange(reviewer, tester, gate, guard);
        ctx.SkillKits.AddRange(kitA, kitB);
        await ctx.SaveChangesAsync();

        return (project.Id, kitA.Id, kitB.Id);
    }

    private static KitActivationService CreateService(OvermindDbContext ctx) =>
        new(ctx, new SkillMaterializer(ctx, PlatformDirectoryMap.Load()));
}
