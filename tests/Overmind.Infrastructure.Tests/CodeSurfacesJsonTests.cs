using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class CodeSurfacesJsonTests
{
    [Fact]
    public void Serialize_EmptyOrNullEntries_ReturnsNull()
    {
        CodeSurfacesJson.Serialize([]).ShouldBeNull();
        CodeSurfacesJson.Serialize(["   ", ""]).ShouldBeNull();
    }

    [Fact]
    public void Serialize_TrimsDedupesAndStoresStableJson()
    {
        var json = CodeSurfacesJson.Serialize([" src/web/** ", "src/web/**"]);

        CodeSurfacesJson.Deserialize(json).ShouldBe(["src/web/**"]);
    }

    [Fact]
    public void Deserialize_NullBlankOrBroken_ReturnsEmpty()
    {
        CodeSurfacesJson.Deserialize(null).ShouldBeEmpty();
        CodeSurfacesJson.Deserialize("").ShouldBeEmpty();
        CodeSurfacesJson.Deserialize("   ").ShouldBeEmpty();
        CodeSurfacesJson.Deserialize("{not json").ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_OverTheEntryCap_Throws()
    {
        var oversized = new string('a', PlanningFeature.MaxCodeSurfaceLength + 1);

        var ex = Should.Throw<ArgumentException>(() => CodeSurfacesJson.Serialize([oversized]));

        ex.Message.ShouldContain("character cap");
    }

    [Fact]
    public void Serialize_OverTheCountCap_Throws()
    {
        var patterns = Enumerable.Range(0, PlanningFeature.MaxCodeSurfaces + 1)
            .Select(i => $"src/area-{i}/**")
            .ToArray();

        var ex = Should.Throw<ArgumentException>(() => CodeSurfacesJson.Serialize(patterns));

        ex.Message.ShouldContain("at most");
    }
}
