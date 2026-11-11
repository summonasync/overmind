using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureOverlapMatchTests
{
    [Fact]
    public void Compute_SamePatternOnBothSides_ClassifiesAsExact()
    {
        var alpha = InFlight("001-alpha", ["README.md"], claimed: true);
        var beta = InFlight("002-beta", ["README.md"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        var alphaMatch = overlaps[alpha.Id].Single().Matches.Single();
        alphaMatch.Pattern.ShouldBe("README.md");
        alphaMatch.PartnerPattern.ShouldBe("README.md");
        alphaMatch.Kind.ShouldBe(FeatureOverlapMatchKind.Exact);
        var betaMatch = overlaps[beta.Id].Single().Matches.Single();
        betaMatch.Kind.ShouldBe(FeatureOverlapMatchKind.Exact);
    }

    [Fact]
    public void Compute_SiblingPatternsOfSharedDirectory_ClassifyAsSharedDirectory()
    {
        var alpha = InFlight("001-alpha", ["tests/A*.cs"], claimed: true);
        var beta = InFlight("002-beta", ["tests/B*.cs"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        var match = overlaps[alpha.Id].Single().Matches.Single();
        match.Pattern.ShouldBe("tests/A*.cs");
        match.PartnerPattern.ShouldBe("tests/B*.cs");
        match.Kind.ShouldBe(FeatureOverlapMatchKind.SharedDirectory);
    }

    [Fact]
    public void Compute_PartnerPatternFollowsLongestSharedLiteralPrefix()
    {
        var alpha = InFlight("001-alpha", ["src/web/**"], claimed: true);
        var beta = InFlight("002-beta", ["src/api/**", "src/web/components/**"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        var match = overlaps[alpha.Id].Single().Matches.Single();
        match.PartnerPattern.ShouldBe("src/web/components/**");
        match.Kind.ShouldBe(FeatureOverlapMatchKind.SharedDirectory);
    }

    [Fact]
    public void Compute_PatternWithoutBoundaryOverlap_NeverEntersMatches()
    {
        var alpha = InFlight("001-alpha", ["src/web/**", "docs/**"], claimed: true);
        var beta = InFlight("002-beta", ["src/web/App.tsx"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        var alphaMatches = overlaps[alpha.Id].Single().Matches;
        alphaMatches.Select(m => m.Pattern).ShouldBe(["src/web/**"]);
        alphaMatches.ShouldNotContain(m => m.Pattern == "docs/**");

        var betaMatches = overlaps[beta.Id].Single().Matches;
        betaMatches.Single().Pattern.ShouldBe("src/web/App.tsx");
        betaMatches.Single().PartnerPattern.ShouldBe("src/web/**");
    }

    [Fact]
    public void Compute_DeterministicMatchOrderAcrossRuns()
    {
        var alpha = InFlight("001-alpha", ["a/**", "b/**"], claimed: true);
        var beta = InFlight("002-beta", ["b/1.cs", "a/1.cs"], claimed: true);

        var first = CodeSurfaceOverlapProjection.Compute([alpha, beta]);
        var second = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        first[alpha.Id].Single().Matches
            .ShouldBe(second[alpha.Id].Single().Matches);
    }

    private static CodeSurfaceFeatureRow InFlight(
        string slug, IReadOnlyList<string>? surfaces, bool claimed,
        PlanningFeatureStatus status = PlanningFeatureStatus.Draft)
    {
        var id = Guid.NewGuid();
        return new CodeSurfaceFeatureRow(
            id, slug, status, claimed ? id : null,
            surfaces is null ? null : CodeSurfacesJson.Serialize(surfaces));
    }
}
