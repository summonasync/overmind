using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class FeatureCodeMergerTests
{
    private static FeatureCodeCandidate Worktree(string name, string path, string? branch = null) =>
        new(FeatureCodeCandidate.WorktreeKind, name, [name], path, branch, null, false);

    private static FeatureCodeCandidate Branch(string name) =>
        new(FeatureCodeCandidate.BranchKind, name, [name], null, name, null, false);

    [Fact]
    public void Merge_NoPins_MatchesByConventionOnly()
    {
        var candidates = new[] { Worktree("feat/134-demo", "/repo/wt"), Branch("main") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, null, null);

        merged.Count.ShouldBe(1);
        merged[0].Name.ShouldBe("feat/134-demo");
        merged[0].Pinned.ShouldBeFalse();
    }

    [Fact]
    public void Merge_PinnedBranchNotMatchingConvention_ListsItFirstFlaggedPinned()
    {
        var candidates = new[] { Branch("main"), Branch("hotfix/renamed-code"), Branch("feat/134-demo") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "hotfix/renamed-code", null);

        merged.Count.ShouldBe(2);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[0].Name.ShouldBe("hotfix/renamed-code");
        merged[0].Pinned.ShouldBeTrue();
        merged[1].Name.ShouldBe("feat/134-demo");
        merged[1].Pinned.ShouldBeFalse();
    }

    [Fact]
    public void Merge_PinnedWorktree_DeduplicatesConventionHit()
    {
        var candidates = new[] { Worktree("feat/134-demo", "/repo/wt"), Branch("feat/134-demo") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "feat/134-demo", "/repo/wt");

        merged.Count.ShouldBe(2);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.WorktreeKind);
        merged[0].Pinned.ShouldBeTrue();
        merged[1].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[1].Name.ShouldBe("feat/134-demo");
        merged[1].Pinned.ShouldBeTrue();
    }

    [Fact]
    public void Merge_StalePinnedBranch_IsReportedPinnedStale()
    {
        var candidates = new[] { Branch("main"), Branch("feat/134-demo") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "feat/swept-away", null);

        merged.Count.ShouldBe(2);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[0].Name.ShouldBe("feat/swept-away");
        merged[0].Path.ShouldBeNull();
        merged[0].Stale.ShouldBeTrue();
        merged[0].Pinned.ShouldBeTrue();
        merged[1].Name.ShouldBe("feat/134-demo");
    }

    [Fact]
    public void Merge_StalePinnedWorktree_KeepsLeafNameAndPath()
    {
        var merged = FeatureCodeMerger.Merge("134-demo", [Branch("main")], null, "/repo/.claude/worktrees/ghost");

        merged.Count.ShouldBe(1);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.WorktreeKind);
        merged[0].Name.ShouldBe("ghost");
        merged[0].Path.ShouldBe("/repo/.claude/worktrees/ghost");
        merged[0].Stale.ShouldBeTrue();
        merged[0].Pinned.ShouldBeTrue();
    }

    [Fact]
    public void Merge_BothPinsStale_ListsStaleBeforeConventionHits()
    {
        var candidates = new[] { Branch("main"), Branch("feat/134-demo") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "feat/elsewhere", "/repo/missing");

        merged.Count.ShouldBe(3);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[0].Name.ShouldBe("feat/elsewhere");
        merged[0].Stale.ShouldBeTrue();
        merged[1].Kind.ShouldBe(FeatureCodeCandidate.WorktreeKind);
        merged[1].Name.ShouldBe("missing");
        merged[1].Stale.ShouldBeTrue();
        merged[2].Name.ShouldBe("feat/134-demo");
        merged[2].Stale.ShouldBeFalse();
    }

    [Fact]
    public void Merge_DetachedWorktreeOnPinnedPath_BranchPinStillStale()
    {
        var candidates = new[] { Worktree("detached-leaf", "/repo/wt") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "feat/134-demo", "/repo/wt");

        merged.Count.ShouldBe(2);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.WorktreeKind);
        merged[0].Name.ShouldBe("detached-leaf");
        merged[0].Pinned.ShouldBeTrue();
        merged[1].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[1].Name.ShouldBe("feat/134-demo");
        merged[1].Stale.ShouldBeTrue();
        merged[1].Pinned.ShouldBeTrue();
    }

    [Fact]
    public void Merge_FullRefPinnedBranch_MatchesShortNameListing()
    {
        var candidates = new[] { Branch("main"), Branch("feat/134-demo") };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, "refs/heads/feat/134-demo", null);

        merged.Count.ShouldBe(1);
        merged[0].Kind.ShouldBe(FeatureCodeCandidate.BranchKind);
        merged[0].Name.ShouldBe("feat/134-demo");
        merged[0].Pinned.ShouldBeTrue();
        merged[0].Stale.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/repo/wt", "/repo/wt/")]
    [InlineData("/repo/wt", "\\repo\\wt")]
    [InlineData("/Repo/Wt", "/repo/wt")]
    public void Merge_PinPathNormalization_MatchesLocalWorktree(string candidatePath, string pinnedPath)
    {
        var candidates = new[] { Worktree("feat/134-demo", candidatePath) };

        var merged = FeatureCodeMerger.Merge("134-demo", candidates, null, pinnedPath);

        merged.Count.ShouldBe(1);
        merged[0].Pinned.ShouldBeTrue();
        merged[0].Stale.ShouldBeFalse();
    }
}
