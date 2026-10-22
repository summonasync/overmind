using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningTaskSectionsTests
{
    [Fact]
    public void Declared_ListsTheEightTaskSectionsInContractOrder()
    {
        PlanningTaskSections.Declared.ShouldBe(
        [
            "goal", "scope", "anti-scope", "constraints", "acceptance-criteria", "refs", "obstacles", "log"
        ]);
    }

    [Theory]
    [InlineData("goal", true)]
    [InlineData("anti-scope", true)]
    [InlineData("obstacles", true)]
    [InlineData("banana", false)]
    [InlineData("Goal", false)]
    public void IsDeclared_MatchesTheVocabularyOrdinaly(string name, bool expected)
    {
        PlanningTaskSections.IsDeclared(name).ShouldBe(expected);
    }

    [Fact]
    public void WithSkeleton_EmptyBody_AppendsEverySectionDeclaredEmpty()
    {
        var body = PlanningTaskSections.WithSkeleton(string.Empty);

        var expected = """
<goal>
(none)
</goal>
<scope>
(none)
</scope>
<anti-scope>
(none)
</anti-scope>
<constraints>
(none)
</constraints>
<acceptance-criteria>
(none)
</acceptance-criteria>
<refs>
(none)
</refs>
<obstacles>
(none)
</obstacles>
<log>
(none)
</log>
""" + "\n";

        body.ShouldBe(expected);
    }

    [Fact]
    public void WithSkeleton_PartialBody_KeepsProvidedSectionsByteIntactAndAppendsTheMissing()
    {
        const string body = "# Task\n\n<goal>\nShip it.\n</goal>\n\n<obstacles>\n- flaky index\n</obstacles>\n";

        var skeletoned = PlanningTaskSections.WithSkeleton(body);

        skeletoned.ShouldStartWith(body);
        PlanningSections.Find("goal", skeletoned)!.Content.ShouldBe("Ship it.");
        PlanningSections.Find("obstacles", skeletoned)!.Content.ShouldBe("- flaky index");
        PlanningSections.Find("scope", skeletoned)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        PlanningSections.Find("log", skeletoned)!.Content.ShouldBe(PlanningSections.EmptyMarker);
        PlanningSections.List(skeletoned).ShouldBe(
        [
            "goal", "obstacles", "scope", "anti-scope", "constraints", "acceptance-criteria", "refs", "log"
        ]);
    }

    [Fact]
    public void WithSkeleton_CompleteBody_IsAStableNoOp()
    {
        var once = PlanningTaskSections.WithSkeleton("<goal>\nShip.\n</goal>");

        PlanningTaskSections.WithSkeleton(once).ShouldBe(once);
    }
}
