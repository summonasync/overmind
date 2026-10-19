using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireBinaryLocatorTests
{
    [Fact]
    public void Locate_OverrideExists_ReturnsOverride()
    {
        var overridePath = Path.Combine("/opt/grimoire", GrimoireBinaryLocator.BinaryName);
        var onPath = Path.Combine("/usr/bin", GrimoireBinaryLocator.BinaryName);
        var locator = new GrimoireBinaryLocator(Exists([overridePath, onPath]), Join("/usr/bin"));

        locator.Locate(overridePath).ShouldBe(overridePath);
    }

    [Fact]
    public void Locate_OverrideMissingFile_FallsBackToPath()
    {
        var onPath = Path.Combine("/usr/bin", GrimoireBinaryLocator.BinaryName);
        var locator = new GrimoireBinaryLocator(Exists([onPath]), Join("/usr/bin"));

        locator.Locate("/gone/grimoire").ShouldBe(onPath);
    }

    [Fact]
    public void Locate_NoOverride_FindsBinaryOnPath()
    {
        var onPath = Path.Combine("/usr/local/bin", GrimoireBinaryLocator.BinaryName);
        var locator = new GrimoireBinaryLocator(Exists([onPath]), Join("/usr/local/bin", "/usr/bin"));

        locator.Locate(null).ShouldBe(onPath);
    }

    [Fact]
    public void Locate_EmptyOverride_ProbesPath()
    {
        var onPath = Path.Combine("/bin", GrimoireBinaryLocator.BinaryName);
        var locator = new GrimoireBinaryLocator(Exists([onPath]), Join("/bin"));

        locator.Locate(string.Empty).ShouldBe(onPath);
    }

    [Fact]
    public void Locate_EmptyPathVariable_ReturnsNull()
    {
        var locator = new GrimoireBinaryLocator(Exists([]), string.Empty);

        locator.Locate(null).ShouldBeNull();
    }

    [Fact]
    public void Locate_NothingMatches_ReturnsNull()
    {
        var locator = new GrimoireBinaryLocator(Exists([]), Join("/usr/bin", "/bin"));

        locator.Locate(null).ShouldBeNull();
    }

    private static string Join(params string[] directories) =>
        string.Join(Path.PathSeparator, directories);

    private static Func<string, bool> Exists(string[] paths) => path => paths.Contains(path);
}
