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
    public void PruneStaleParentSnapshots_DropsDeadParentsAndKeepsEverythingElse()
    {
        Stamp("parent-4242.json");
        Stamp("parent-99.json");
        Stamp("workspace-abc123.json");
        Stamp("parent-nobody.json");

        var pruned = SessionIdentityFiles.PruneStaleParentSnapshots(_root, pid => pid != 4242);

        pruned.ShouldBe(1);
        Exists("parent-4242.json").ShouldBeFalse();
        Exists("parent-99.json").ShouldBeTrue();
        Exists("workspace-abc123.json").ShouldBeTrue();
        Exists("parent-nobody.json").ShouldBeTrue();
    }

    [Fact]
    public void PruneStaleParentSnapshots_LiveProcessProbeKeepsTheOwnParentStamp()
    {
        Stamp($"parent-{Environment.ProcessId}.json");
        Stamp("parent-2147483000.json");

        SessionIdentityFiles.PruneStaleParentSnapshots(_root);

        Exists($"parent-{Environment.ProcessId}.json").ShouldBeTrue();
        Exists("parent-2147483000.json").ShouldBeFalse();
    }

    [Fact]
    public void PruneStaleParentSnapshots_AbsentDirectory_IsQuiet()
    {
        SessionIdentityFiles.PruneStaleParentSnapshots(
            Path.Combine(_root, "never-created")).ShouldBe(0);
    }

    private void Stamp(string fileName)
    {
        Directory.CreateDirectory(IdentityDirectory());
        File.WriteAllText(Path.Combine(IdentityDirectory(), fileName), "{}");
    }

    private bool Exists(string fileName) => File.Exists(Path.Combine(IdentityDirectory(), fileName));

    private string IdentityDirectory() => Path.Combine(_root, "identity");
}
