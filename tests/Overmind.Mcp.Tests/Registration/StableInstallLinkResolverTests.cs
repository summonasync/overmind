using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class StableInstallLinkResolverTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-stable-{Guid.NewGuid():N}");
    private readonly string _installDir;
    private readonly string _stableLink;

    public StableInstallLinkResolverTests()
    {
        _installDir = Path.Combine(_home, "apps", "overmind", "mcp");
        Directory.CreateDirectory(_installDir);
        _stableLink = Path.Combine(_installDir, "overmind-mcp");
    }

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
    }

    private AgentPathEnvironment Env() =>
        new() { HomeDirectory = _home, Variables = new Dictionary<string, string>() };

    private string CreateBinary(string relativePath)
    {
        var path = Path.Combine(_home, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "binary");
        return path;
    }

    [Fact]
    public void StableLinkResolvingToRunningBinary_ReturnsLinkPath()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-1.2.3", "overmind-mcp"));
        File.CreateSymbolicLink(_stableLink, binary);

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(binary, Env());

        result.ShouldBe(_stableLink);
    }

    [Fact]
    public void NoStableLink_ReturnsRealPath()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-mcp"));

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(binary, Env());

        result.ShouldBe(binary);
    }

    [Fact]
    public void StableLinkPointingAtOtherBinary_ReturnsRealPath()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-mcp"));
        var other = CreateBinary(Path.Combine("dist", "some-other-tool"));
        File.CreateSymbolicLink(_stableLink, other);

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(binary, Env());

        result.ShouldBe(binary);
    }

    [Fact]
    public void DanglingStableLink_ReturnsRealPath()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-mcp"));
        File.CreateSymbolicLink(_stableLink, Path.Combine(_home, "gone", "overmind-mcp"));

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(binary, Env());

        result.ShouldBe(binary);
    }

    [Fact]
    public void PlainFileAtStableLinkLocation_NotTrusted_ReturnsRealPath()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-mcp"));
        File.WriteAllText(_stableLink, "impostor copy");

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(binary, Env());

        result.ShouldBe(binary);
    }

    [Fact]
    public void RunningBinaryThroughIntermediaryLink_StillMatchesStableLink()
    {
        var binary = CreateBinary(Path.Combine("dist", "overmind-2.0.0", "overmind-mcp"));
        var shimDir = Path.Combine(_home, "shim");
        Directory.CreateDirectory(shimDir);
        var shim = Path.Combine(shimDir, "overmind-mcp");
        File.CreateSymbolicLink(shim, binary);
        File.CreateSymbolicLink(_stableLink, binary);

        var result = StableInstallLinkResolver.ResolvePreferredBinaryPath(shim, Env());

        result.ShouldBe(_stableLink);
    }
}
