using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class GitRootResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overmind-git-{Guid.NewGuid():N}");

    public GitRootResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FindRoot_StartDirectoryIsGitRoot_ReturnsIt()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        GitRootResolver.FindRoot(_root).ShouldBe(_root);
    }

    [Fact]
    public void FindRoot_WalksUpFromNestedDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var nested = Path.Combine(_root, "src", "deep", "deeper");
        Directory.CreateDirectory(nested);

        GitRootResolver.FindRoot(nested).ShouldBe(_root);
    }

    [Fact]
    public void FindRoot_GitFileMarker_WorktreeLayout_Recognized()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git-worktree-parent", ".git"));
        var worktree = Path.Combine(_root, "worktree");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: ../.git-worktree-parent/.git/worktrees/x");

        GitRootResolver.FindRoot(Path.Combine(worktree, "sub")).ShouldBe(worktree);
    }

    [Fact]
    public void FindRoot_NoGitMarker_ReturnsNull()
    {
        var orphan = Path.Combine(Path.GetTempPath(), $"overmind-nogit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(orphan);

        try
        {
            GitRootResolver.FindRoot(orphan).ShouldBeNull();
        }
        finally
        {
            Directory.Delete(orphan, recursive: true);
        }
    }

    [Fact]
    public void FindRoot_NearestGitRootWins()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var inner = Path.Combine(_root, "inner");
        Directory.CreateDirectory(Path.Combine(inner, ".git"));

        GitRootResolver.FindRoot(Path.Combine(inner, "deep")).ShouldBe(inner);
    }
}
