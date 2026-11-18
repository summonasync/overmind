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

    [Theory]
    [InlineData(PlanningLogSource.Ui)]
    [InlineData(PlanningLogSource.Agent)]
    public async Task Relocate_FeatureWithExistingLog_AppendsExactlyOneProvenanceLineOfTheAskedSource(
        PlanningLogSource source)
    {
        var feature = AddFeature("006-logged", "feat/006", $"{_oldRoot}/wt");
        var before = PlanningLog.Parse(FeatureBody(feature.Slug)).Count;
        before.ShouldBeGreaterThan(0);

        await RelocateAsync(source);

        var lines = PlanningLog.Parse(FeatureBody(feature.Slug));
        lines.Count.ShouldBe(before + 1);
        lines.Count(line => line.Text.Contains(Marker, StringComparison.Ordinal)).ShouldBe(1);
        lines[^1].Text.ShouldContain($"'{_oldRoot}/wt'");
        lines[^1].Text.ShouldContain($"'{_newRoot}/wt'");
        lines[^1].Source.ShouldBe(source);
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
            _context, _project, oldRoot, newRoot, PlanningLogSource.Ui, CancellationToken.None);

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
            _context, _project, _oldRoot, _oldRoot, PlanningLogSource.Ui, CancellationToken.None);

        relocations.ShouldBeEmpty();
        Feature(feature.Slug).PinnedWorktreePath.ShouldBe($"{_oldRoot}/wt");
        PlanningLog.Parse(FeatureBody(feature.Slug)).Any(line => line.Text.Contains(Marker, StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Relocate_MutatesTrackedRowsOnly_LeavesTheCommitToTheCaller()
    {
        var feature = AddFeature("012-uncommitted", "feat/012", $"{_oldRoot}/wt");
        var pinnedPath = $"{_oldRoot}/wt";

        var relocations = await ProjectPinRelocator.RelocateWorktreePinsAsync(
            _context, WithDiskPath(_project, _newRoot), _oldRoot, _newRoot, PlanningLogSource.Ui,
            CancellationToken.None);

        relocations.ShouldHaveSingleItem().Slug.ShouldBe(feature.Slug);

        using var beforeCommit = _db.CreateContext();
        (await beforeCommit.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == feature.Slug))
            .PinnedWorktreePath.ShouldBe(pinnedPath);
        (await beforeCommit.Projects.AsNoTracking().SingleAsync(p => p.Id == _project.Id))
            .DiskPath.ShouldBe(_oldRoot);

        await _context.SaveChangesAsync(CancellationToken.None);

        using var afterCommit = _db.CreateContext();
        var persisted = await afterCommit.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == feature.Slug);
        persisted.PinnedWorktreePath.ShouldBe($"{_newRoot}/wt");
        CountMarkers(persisted.Body).ShouldBe(1);
        (await afterCommit.Projects.AsNoTracking().SingleAsync(p => p.Id == _project.Id))
            .DiskPath.ShouldBe(_newRoot);
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

    private Task<IReadOnlyList<PinRelocation>> RelocateAsync() => RelocateAsync(PlanningLogSource.Ui);

    private async Task<IReadOnlyList<PinRelocation>> RelocateAsync(PlanningLogSource source)
    {
        var relocations = await ProjectPinRelocator.RelocateWorktreePinsAsync(
            _context, WithDiskPath(_project, _newRoot), _oldRoot, _newRoot, source, CancellationToken.None);

        await _context.SaveChangesAsync(CancellationToken.None);
        return relocations;
    }

    private static int CountMarkers(string body) =>
        PlanningLog.Parse(body).Count(line => line.Text.Contains(Marker, StringComparison.Ordinal));

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
