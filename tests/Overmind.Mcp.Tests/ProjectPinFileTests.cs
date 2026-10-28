using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class ProjectPinFileTests : IDisposable
{
    private static readonly Guid PinId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OuterPinId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly string Workspace = Directory.CreateTempSubdirectory("overmind-pin-file").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Workspace, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Read_PinAtGitRoot_FoundFromNestedCwd()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        Directory.CreateDirectory(Path.Combine(Workspace, "src", "deep"));
        WritePin(Workspace, PinId.ToString());

        var pin = ProjectPinFile.Read(Path.Combine(Workspace, "src", "deep"));

        pin.ProjectId.ShouldBe(PinId);
        pin.IsUnparsable.ShouldBeFalse();
    }

    [Fact]
    public void Read_JsonProjectIdForm_ParsesGuid()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin(Workspace, $$"""{ "projectId": "{{PinId}}" }""");

        ProjectPinFile.Read(Workspace).ProjectId.ShouldBe(PinId);
    }

    [Fact]
    public void Read_CorruptPin_ReportsFilePathWithoutId()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin(Workspace, "leftover garbage");

        var pin = ProjectPinFile.Read(Workspace);

        pin.ProjectId.ShouldBeNull();
        pin.FilePath.ShouldBe(Path.Combine(Workspace, ".overmind", "project"));
        pin.IsUnparsable.ShouldBeTrue();
    }

    [Fact]
    public void Read_EmptyPin_Unparsable()
    {
        Directory.CreateDirectory(Path.Combine(Workspace, ".git"));
        WritePin(Workspace, string.Empty);

        ProjectPinFile.Read(Workspace).IsUnparsable.ShouldBeTrue();
    }

    [Fact]
    public void Read_NestedWorktree_DoesNotInheritOuterPin()
    {
        var outer = Path.Combine(Workspace, "outer");
        var worktree = Path.Combine(outer, "wt");
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        Directory.CreateDirectory(worktree);
        WritePin(outer, OuterPinId.ToString());
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {outer}/.git/worktrees/wt\n");

        ProjectPinFile.Read(worktree).ProjectId.ShouldBeNull();
        ProjectPinFile.Read(Path.Combine(worktree, "src")).ProjectId.ShouldBeNull();
        ProjectPinFile.Read(Path.Combine(outer, "src")).ProjectId.ShouldBe(OuterPinId);
    }

    [Fact]
    public void Read_WorktreeRootPin_FoundFromWorktreeSubdir()
    {
        var worktree = Path.Combine(Workspace, "wt");
        Directory.CreateDirectory(worktree);
        Directory.CreateDirectory(Path.Combine(worktree, "src"));
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {Workspace}/.git/worktrees/wt\n");
        WritePin(worktree, PinId.ToString());

        ProjectPinFile.Read(Path.Combine(worktree, "src")).ProjectId.ShouldBe(PinId);
    }

    [Fact]
    public void Read_OutsideRepo_OnlyCwdChecked()
    {
        var baseDirectory = Path.Combine(Workspace, "repo-less");
        var cwd = Path.Combine(baseDirectory, "cwd");
        Directory.CreateDirectory(cwd);
        WritePin(baseDirectory, PinId.ToString());

        var pin = ProjectPinFile.Read(cwd);

        pin.ProjectId.ShouldBeNull();
        pin.FilePath.ShouldBeNull();
    }

    private void WritePin(string directory, string content)
    {
        Directory.CreateDirectory(Path.Combine(directory, ".overmind"));
        File.WriteAllText(Path.Combine(directory, ".overmind", "project"), content);
    }
}
