using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FrontmatterKeyPaletteTests
{
    [Fact]
    public void Colors_AreDistinct()
    {
        FrontmatterKeyPalette.Colors.Distinct().Count().ShouldBe(FrontmatterKeyPalette.Colors.Count);
    }

    [Fact]
    public void ColorFor_ArbitraryNames_ReturnPaletteColors()
    {
        var names = new[]
        {
            "x", "unknown_thing", "zzz", "due_date", "priority", "owner", "estimate", "milestone", "sprint"
        };

        foreach (var name in names)
        {
            FrontmatterKeyPalette.Colors.ShouldContain(FrontmatterKeyPalette.ColorFor(name));
        }
    }

    [Fact]
    public void ColorFor_IgnoresCaseAndSurroundingWhitespace()
    {
        var expected = FrontmatterKeyPalette.ColorFor("some_key");

        FrontmatterKeyPalette.ColorFor("SOME_KEY").ShouldBe(expected);
        FrontmatterKeyPalette.ColorFor("  Some_Key  ").ShouldBe(expected);
    }
}
