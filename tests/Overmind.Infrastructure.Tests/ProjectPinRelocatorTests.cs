using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ProjectPinRelocatorTests : IDisposable
{
    private const string Marker = "code pin moved with project root";

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Project _project;
    private readonly string _oldRoot;
    private readonly string _newRoot;

    public ProjectPinRelocatorTests()
    {
        _context = _db.CreateContext();

        var stamp = Guid.NewGuid().ToString("N");
        _oldRoot = $"/var/overmind-pins/{stamp}/old";
        _newRoot = $"/var/overmind-pins/{stamp}/moved";

        _project = Project.Create($"pins-{stamp}", _oldRoot);
        _context.Projects.Add(_project);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Relocate_PinsUnderOldRoot_RemapTailAndPersist()
    {
        var lane = AddFeature("001-lane", "feat/001", $"{_oldRoot}/.lanes/001");
        var nested = AddFeature("002-nested", null, $"{_oldRoot}/worktrees/deep/002");

        var relocations = await RelocateAsync();

        relocations.Select(r => r.Slug).ShouldBe([lane.Slug, nested.Slug], ignoreOrder: true);
        relocations.Single(r => r.Slug == lane.Slug).To.ShouldBe($"{_newRoot}/.lanes/001");

        using var reloaded = _db.CreateContext();
        var moved = reloaded.PlanningFeatures.Single(f => f.Slug == lane.Slug);
        moved.PinnedWorktreePath.ShouldBe($"{_newRoot}/.lanes/001");
        moved.PinnedBranch.ShouldBe("feat/001");
        reloaded.PlanningFeatures.Single(f => f.Slug == nested.Slug)
            .PinnedWorktreePath.ShouldBe($"{_newRoot}/worktrees/deep/002");
    }

    [Fact]
    public async Task Relocate_ForeignAndSiblingPrefixPins_StayUntouched()
    {
        var foreign = AddFeature("003-foreign", null, "/var/other-product/wt");
        var sibling = AddFeature("004-sibling", null, $"{_oldRoot}-backup/wt");
        var suffixTrap = AddFeature("005-suffix", null, _oldRoot + "X");

        var relocations = await RelocateAsync();

        relocations.ShouldBeEmpty();
        using var reloaded = _db.CreateContext();
        reloaded.PlanningFeatures.Single(f => f.Slug == foreign.Slug).PinnedWorktreePath
            .ShouldBe("/var/other-product/wt");
        reloaded.PlanningFeatures.Single(f => f.Slug == sibling.Slug).PinnedWorktreePath
            .ShouldBe($"{_oldRoot}-backup/wt");
        reloaded.PlanningFeatures.Single(f => f.Slug == suffixTrap.Slug).PinnedWorktreePath
            .ShouldBe(_oldRoot + "X");
    }

    [Fact]
    public async Task Relocate_FeatureWithExistingLog_AppendsExactlyOneProvenanceLine()
    {
        var feature = AddFeature("006-logged", "feat/006", $"{_oldRoot}/wt");
        var before = PlanningLog.Parse(FeatureBody(feature.Slug)).Count;
        before.ShouldBeGreaterThan(0);

        await RelocateAsync();

        var lines = PlanningLog.Parse(FeatureBody(feature.Slug));
        lines.Count.ShouldBe(before + 1);
        lines.Count(line => line.Text.Contains(Marker, StringComparison.Ordinal)).ShouldBe(1);
        lines[^1].Text.ShouldContain($"'{_oldRoot}/wt'");
        lines[^1].Text.ShouldContain($"'{_newRoot}/wt'");
        lines[^1].Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public async Task Relocate_PinNamingTheProjectRoot_BecomesNewRoot()
    {
        var feature = AddFeature("007-root-pin", null, _oldRoot);

        await RelocateAsync();

        Feature(feature.Slug).PinnedWorktreePath.ShouldBe(_newRoot);
    }

    [Theory]
    [InlineData(
        @"E:\Projects\App",
        @"E:\Projects\AppMoved",
        @"E:\Projects\App\.lanes\8",
        @"E:\Projects\AppMoved\.lanes\8")]
    [InlineData(
        @"E:\Projects\App",
        @"E:\Projects\AppMoved",
        "E:/Projects/App/.lanes/8",
        @"E:\Projects\AppMoved/.lanes/8")]
    [InlineData("/var/elsewhere/old", "/var/elsewhere/new", "/var/elsewhere/old/wt", "/var/elsewhere/new/wt")]
    public async Task Relocate_PinUnderOldRoot_RemapsRegardlessOfSeparatorStyle(
        string oldRoot, string newRoot, string pin, string expected)
    {
        var feature = AddFeature("008-foreign-style", null, pin);

        var relocations = await ProjectPinRelocator.RelocateWorktreePinsAsync(
            _context, WithDiskPath(_project, newRoot), oldRoot, CancellationToken.None);

        var relocated = relocations.ShouldHaveSingleItem();
        relocated.Slug.ShouldBe(feature.Slug);
        relocated.From.ShouldBe(pin);
        relocated.To.ShouldBe(expected);
    }

    [Fact]
    public async Task Relocate_UnchangedRoot_RewritesNothing()
    {
        var feature = AddFeature("009-same-root", null, $"{_oldRoot}/wt");

        var relocations = await ProjectPinRelocator.RelocateWorktreePinsAsync(
            _context, _project, _oldRoot, CancellationToken.None);

        relocations.ShouldBeEmpty();
        Feature(feature.Slug).PinnedWorktreePath.ShouldBe($"{_oldRoot}/wt");
        PlanningLog.Parse(FeatureBody(feature.Slug)).Any(line => line.Text.Contains(Marker, StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Relocate_SecondProjectPinUnderSameRoot_StaysUntouched()
    {
        var mine = AddFeature("010-mine", null, $"{_oldRoot}/wt");
        var neighbour = Project.Create($"neighbour-{Guid.NewGuid():N}", "/var/elsewhere");
        _context.Projects.Add(neighbour);
        _context.SaveChanges();
        var theirs = AddFeature(neighbour.Id, "011-theirs", null, $"{_oldRoot}/wt");

        var relocations = await RelocateAsync();

        relocations.ShouldHaveSingleItem().Slug.ShouldBe(mine.Slug);
        Feature(theirs.Slug).PinnedWorktreePath.ShouldBe($"{_oldRoot}/wt");
    }

    private async Task<IReadOnlyList<PinRelocation>> RelocateAsync()
    {
        var previousDiskPath = _project.DiskPath;
        WithDiskPath(_project, _newRoot);
        await _context.SaveChangesAsync();

        return await ProjectPinRelocator.RelocateWorktreePinsAsync(
            _context, _project, previousDiskPath, CancellationToken.None);
    }

    private Project WithDiskPath(Project project, string diskPath)
    {
        _context.Entry(project).Property(p => p.DiskPath).CurrentValue = diskPath;
        return project;
    }

    private PlanningFeature AddFeature(string slug, string? branch, string worktreePath) =>
        AddFeature(_project.Id, slug, branch, worktreePath);

    private PlanningFeature AddFeature(Guid projectId, string slug, string? branch, string worktreePath)
    {
        var feature = PlanningFeature.Create(projectId, slug, slug, $"<goal>{slug}</goal>\n");
        feature.SetCodePin(branch, worktreePath, PlanningLogSource.Agent);
        _context.PlanningFeatures.Add(feature);
        _context.SaveChanges();
        return feature;
    }

    private PlanningFeature Feature(string slug) =>
        _context.PlanningFeatures.AsNoTracking().Single(f => f.Slug == slug);

    private string FeatureBody(string slug) => Feature(slug).Body;
}
