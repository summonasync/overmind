using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class OvermindCliLocatorTests
{
    private const string Base = "/apps/overmind-0.1.9/host";

    [Theory]
    [InlineData("/apps/overmind-0.1.9/mcp/overmind-mcp", "/apps/overmind-0.1.9/mcp/overmind-mcp")]
    [InlineData("/usr/local/bin/overmind-mcp", "/apps/overmind-0.1.9/mcp/overmind-mcp")]
    [InlineData("/usr/local/bin/overmind-mcp", null)]
    public void Locate_ResolvesPackedLayoutBeforePath(string onPath, string? packed)
    {
        string[] existing = packed is null ? [onPath] : [packed, onPath];
        var locator = new OvermindCliLocator(
            Base, candidate => existing.Contains(candidate), "/usr/local/bin:/usr/bin");

        locator.Locate().ShouldBe(packed ?? onPath);
    }

    [Fact]
    public void Locate_NothingFound_ReturnsNull()
    {
        var locator = new OvermindCliLocator(Base, _ => false, "/usr/bin");

        locator.Locate().ShouldBeNull();
    }

    [Fact]
    public void Locate_EmptyPathVariable_FallsBackToNull()
    {
        var locator = new OvermindCliLocator(Base, _ => false, null);

        locator.Locate().ShouldBeNull();
    }
}
