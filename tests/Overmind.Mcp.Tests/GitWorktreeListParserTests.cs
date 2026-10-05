using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class GitWorktreeListParserTests
{
    private const string PorcelainOutput =
        """
        worktree /repo/main
        HEAD f67669fabc1234567890abcdef1234567890abcd
        branch refs/heads/main

        worktree /repo/.claude/worktrees/063-worktree-feature-binding
        HEAD 0123456789abcdef0123456789abcdef01234567
        branch refs/heads/feat/063-worktree-feature-binding

        worktree /repo/store
        bare

        worktree /repo/detached-review
        HEAD deadbeefdeadbeefdeadbeefdeadbeefdeadbeef
        detached

        """;

    [Fact]
    public void Parse_MixedPorcelain_SplitsBlocksIntoWorktrees()
    {
        var worktrees = GitWorktreeListParser.Parse(PorcelainOutput);

        worktrees.Count.ShouldBe(4);
    }

    [Fact]
    public void Parse_BranchWorktree_StripsRefsHeadsPrefix()
    {
        var worktrees = GitWorktreeListParser.Parse(PorcelainOutput);

        var feature = worktrees[1];
        feature.Path.ShouldBe("/repo/.claude/worktrees/063-worktree-feature-binding");
        feature.Branch.ShouldBe("feat/063-worktree-feature-binding");
        feature.Head.ShouldBe("0123456789abcdef0123456789abcdef01234567");
        feature.IsBare.ShouldBeFalse();
        feature.IsDetached.ShouldBeFalse();
    }

    [Fact]
    public void Parse_BareWorktree_HasNoBranchOrHead()
    {
        var worktrees = GitWorktreeListParser.Parse(PorcelainOutput);

        var bare = worktrees[2];
        bare.Path.ShouldBe("/repo/store");
        bare.IsBare.ShouldBeTrue();
        bare.Branch.ShouldBeNull();
        bare.Head.ShouldBeNull();
    }

    [Fact]
    public void Parse_DetachedWorktree_CarriesHeadWithoutBranch()
    {
        var worktrees = GitWorktreeListParser.Parse(PorcelainOutput);

        var detached = worktrees[3];
        detached.Path.ShouldBe("/repo/detached-review");
        detached.IsDetached.ShouldBeTrue();
        detached.Branch.ShouldBeNull();
        detached.Head.ShouldBe("deadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        GitWorktreeListParser.Parse(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_TrailingBlockWithoutSeparator_IsKept()
    {
        var worktrees = GitWorktreeListParser.Parse(
            "worktree /repo/last\nHEAD abc123\nbranch refs/heads/feat/063-x\n");

        worktrees.Count.ShouldBe(1);
        worktrees[0].Branch.ShouldBe("feat/063-x");
    }
}
