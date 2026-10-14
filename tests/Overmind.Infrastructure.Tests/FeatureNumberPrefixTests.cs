using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureNumberPrefixTests
{
    [Theory]
    [InlineData("042-x", 42)]
    [InlineData("42-x", 42)]
    [InlineData("7-trailing-dashes-x", 7)]
    public void TryParse_PrefixedSlug_ReturnsNumericValue(string slug, int expected)
    {
        FeatureNumberPrefix.TryParse(slug, out var number).ShouldBeTrue();
        number.ShouldBe(expected);
    }

    [Theory]
    [InlineData("x-42")]
    [InlineData("42")]
    [InlineData("numberless")]
    public void TryParse_WithoutLeadingNumber_ReturnsFalse(string slug)
    {
        FeatureNumberPrefix.TryParse(slug, out _).ShouldBeFalse();
    }
}
