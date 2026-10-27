using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class GitRepositoryProbeTests : IDisposable
{
    private readonly string Root = Directory.CreateTempSubdirectory("overmind-probe").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void PlainDirectory_IsNotARepository()
    {
        GitRepositoryProbe.IsRepository(Root).ShouldBeFalse();
    }

    [Fact]
    public void PlainDirectory_ProbeIsFast_NoGitProcessBudget()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        GitRepositoryProbe.IsRepository(Root);
        watch.Stop();

        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GitDirectory_IsARepository()
    {
        Directory.CreateDirectory(Path.Combine(Root, ".git"));

        GitRepositoryProbe.IsRepository(Root).ShouldBeTrue();
    }

    [Fact]
    public void GitFile_IsARepository_WorktreeLayout()
    {
        File.WriteAllText(Path.Combine(Root, ".git"), "gitdir: /elsewhere/repo/.git/worktrees/demo");

        GitRepositoryProbe.IsRepository(Root).ShouldBeTrue();
    }

    [Fact]
    public void NestedPath_WalksUpToRepositoryRoot()
    {
        Directory.CreateDirectory(Path.Combine(Root, ".git"));
        var nested = Directory.CreateDirectory(Path.Combine(Root, "deep", "deeper")).FullName;

        GitRepositoryProbe.IsRepository(nested).ShouldBeTrue();
    }

    [Fact]
    public void BareRepositoryLayout_IsARepository()
    {
        Directory.CreateDirectory(Path.Combine(Root, "objects"));
        Directory.CreateDirectory(Path.Combine(Root, "refs"));
        File.WriteAllText(Path.Combine(Root, "HEAD"), "ref: refs/heads/main");

        GitRepositoryProbe.IsRepository(Root).ShouldBeTrue();
    }

    [Fact]
    public void MissingPath_DoesNotThrow()
    {
        var missing = Path.Combine(Root, "does", "not", "exist");

        GitRepositoryProbe.IsRepository(missing).ShouldBeFalse();
    }
}
