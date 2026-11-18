using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class SessionIdentityFilesPruneTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("overmind-identity-prune").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void PruneStaleIdentityFiles_DropsDeadParentsAndKeepsEverythingElse()
    {
        Stamp("parent-4242.json");
        Stamp("parent-99.json");
        Stamp("workspace-abc123.json");
        Stamp("parent-nobody.json");

        var pruned = SessionIdentityFiles.PruneStaleIdentityFiles(_root, pid => pid != 4242);

        pruned.ShouldBe(1);
        Exists("parent-4242.json").ShouldBeFalse();
        Exists("parent-99.json").ShouldBeTrue();
        Exists("workspace-abc123.json").ShouldBeTrue();
        Exists("parent-nobody.json").ShouldBeTrue();
    }

    [Fact]
    public void PruneStaleIdentityFiles_LiveProcessProbeKeepsTheOwnParentStamp()
    {
        Stamp($"parent-{Environment.ProcessId}.json");
        Stamp("parent-2147483000.json");

        SessionIdentityFiles.PruneStaleIdentityFiles(_root);

        Exists($"parent-{Environment.ProcessId}.json").ShouldBeTrue();
        Exists("parent-2147483000.json").ShouldBeFalse();
    }

    [Fact]
    public void PruneStaleIdentityFiles_AbsentDirectory_IsQuiet()
    {
        SessionIdentityFiles.PruneStaleIdentityFiles(
            Path.Combine(_root, "never-created")).ShouldBe(0);
    }

    [Fact]
    public void PruneStaleIdentityFiles_SweepsStrandedWriteTempsOfBothShapes()
    {
        var stranded = Stamp("parent-4242.json.3f2a1b0c9d8e7f605142a3b4c5d6e7f8.tmp");
        var workspaceTemp = Stamp("workspace-abc123.json.1f2e3d4c5b6a798897a6b5c4d3e2f100.tmp");
        SetAncient(stranded);
        SetAncient(workspaceTemp);

        var pruned = SessionIdentityFiles.PruneStaleIdentityFiles(
            _root, pid => pid != 4242, TimeSpan.FromMinutes(2));

        pruned.ShouldBe(2);
        Exists(Path.GetFileName(stranded)).ShouldBeFalse();
        Exists(Path.GetFileName(workspaceTemp)).ShouldBeFalse();
    }

    [Fact]
    public void PruneStaleIdentityFiles_LeavesAnInFlightWriteAndForeignTempsAlone()
    {
        var inFlight = Stamp("parent-4242.json.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.tmp");
        var foreign = Stamp("scratch.tmp");
        SetAncient(foreign);
        Stamp("parent-4242.json");

        var pruned = SessionIdentityFiles.PruneStaleIdentityFiles(
            _root, pid => false, TimeSpan.FromMinutes(2));

        pruned.ShouldBe(1);
        Exists(Path.GetFileName(inFlight)).ShouldBeTrue();
        Exists("scratch.tmp").ShouldBeTrue();
        Exists("parent-4242.json").ShouldBeFalse();
    }

    [Fact]
    public void PruneStaleIdentityFiles_NeverTouchesWorkspaceStampsHoweverOldTheyLook()
    {
        var abandoned = Stamp("workspace-deadbeef.json");
        SetAncient(abandoned);
        Stamp("parent-4242.json");

        var pruned = SessionIdentityFiles.PruneStaleIdentityFiles(_root, _ => false);

        pruned.ShouldBe(1);
        Exists("workspace-deadbeef.json").ShouldBeTrue();
        Exists("parent-4242.json").ShouldBeFalse();
    }

    private string Stamp(string fileName)
    {
        Directory.CreateDirectory(IdentityDirectory());
        var path = Path.Combine(IdentityDirectory(), fileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    private static void SetAncient(string path) =>
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - TimeSpan.FromDays(3));

    private bool Exists(string fileName) => File.Exists(Path.Combine(IdentityDirectory(), fileName));

    private string IdentityDirectory() => Path.Combine(_root, "identity");
}
