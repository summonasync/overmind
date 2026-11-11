using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ClaimGuardOverlapPin
{
    [Theory]
    [InlineData("src/web/Program.cs", "src/web/Program.cs", true)]
    [InlineData("src/web/**", "src/web/App.tsx", true)]
    [InlineData("src/web", "src/web/**", true)]
    [InlineData("src/*.csproj", "src/api/**/*.cs", true)]
    [InlineData("**/*.cs", "src/**", true)]
    [InlineData("*.cs", "*.ts", true)]
    [InlineData("tests/A*.cs", "tests/B*.cs", true)]
    public void GlobsOverlap_ClaimGuardMatchingStaysConservative(
        string left, string right, bool expected)
    {
        CodeSurfaceOverlap.GlobsOverlap(left, right).ShouldBe(expected);
    }

    [Theory]
    [InlineData("src/web/**", "src/webapp/**")]
    [InlineData("src/web/**", "src/api/**")]
    [InlineData("src/a.cs", "src/a.csx")]
    [InlineData("src/a.cs", "src/a.cs.b")]
    public void GlobsOverlap_ClaimGuardSiblingsStayDisjoint(string left, string right)
    {
        CodeSurfaceOverlap.GlobsOverlap(left, right).ShouldBeFalse();
    }

    [Fact]
    public void GlobsOverlap_ClaimGuardMatchingStaysCaseSensitive()
    {
        CodeSurfaceOverlap.GlobsOverlap("src/web/**", "SRC/Web/**").ShouldBeFalse();
        CodeSurfaceOverlap.GlobsOverlap("README.md", "readme.md").ShouldBeFalse();
    }

    [Fact]
    public void SetsOverlap_ClaimGuardSharedDirectoryStillCollides()
    {
        CodeSurfaceOverlap.SetsOverlap(["tests/A*.cs"], ["tests/B*.cs"]).ShouldBeTrue();
    }

    [Fact]
    public void SetsOverlap_ClaimGuardDisjointDirectoriesStayDisjoint()
    {
        CodeSurfaceOverlap.SetsOverlap(["src/web/**"], ["src/api/**"]).ShouldBeFalse();
    }
}
