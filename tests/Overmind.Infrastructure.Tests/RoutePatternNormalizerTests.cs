using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class RoutePatternNormalizerTests
{
    [Theory]
    [InlineData("Show Me The Wave-Chat!", "show me the wave chat")]
    [InlineData("  HOW   to\tdeploy  ", "how to deploy")]
    [InlineData("...", "")]
    [InlineData("Покажи очередь, пожалуйста", "покажи очередь пожалуйста")]
    [InlineData("status???!!!", "status")]
    [InlineData("fix the EF Core migration", "fix the ef core migration")]
    public void Normalize_CollapsesCasePunctuationAndWhitespace(string raw, string expected)
    {
        RoutePatternNormalizer.Normalize(raw).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_IsDeterministicAcrossCalls()
    {
        var first = RoutePatternNormalizer.Normalize(" What Is The Board Status?  ");
        var second = RoutePatternNormalizer.Normalize("what is the board status");

        first.ShouldBe(second);
    }

    [Fact]
    public void Normalize_Blank_Throws()
    {
        Should.Throw<ArgumentException>(() => RoutePatternNormalizer.Normalize("   "));
    }
}
