using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class CodeSurfaceOverlapTests
{
    [Theory]
    [InlineData("src/web/Program.cs", "src/web/Program.cs", true)]
    [InlineData("src/web/**", "src/web/App.tsx", true)]
    [InlineData("src/web", "src/web/**", true)]
    [InlineData("src/web/**", "src/web/**/*.ts", true)]
    [InlineData("src/*.csproj", "src/api/**/*.cs", true)]
    [InlineData("src/*.csproj", "src/**", true)]
    [InlineData("**/*.cs", "src/**", true)]
    [InlineData("*.cs", "*.ts", true)]
    [InlineData("src", "src/web/x.cs", true)]
    public void GlobsOverlap_ConservativeMatches(string left, string right, bool expected)
    {
        CodeSurfaceOverlap.GlobsOverlap(left, right).ShouldBe(expected);
    }

    [Theory]
    [InlineData("src/web/**", "src/webapp/**")]
    [InlineData("src/web", "src/webapp")]
    [InlineData("src/a.cs", "src/a.csx")]
    [InlineData("src/a.cs", "src/a.cs.b")]
    [InlineData("src/web/**", "src/api/**")]
    [InlineData("src/web/Program.cs", "src/api/Program.cs")]
    [InlineData("web/src/**", "docs/src/**")]
    public void GlobsOverlap_SiblingSurfacesStayDisjoint(string left, string right)
    {
        CodeSurfaceOverlap.GlobsOverlap(left, right).ShouldBeFalse();
    }

    [Fact]
    public void GlobsOverlap_IsCaseSensitive()
    {
        CodeSurfaceOverlap.GlobsOverlap("src/web/**", "SRC/Web/**").ShouldBeFalse();
        CodeSurfaceOverlap.GlobsOverlap("README.md", "readme.md").ShouldBeFalse();
    }

    [Fact]
    public void LiteralPrefix_StripsAtFirstWildcardSegment()
    {
        CodeSurfaceOverlap.LiteralPrefix("src/web/**").ShouldBe("src/web/");
        CodeSurfaceOverlap.LiteralPrefix("src/*.csproj").ShouldBe("src/");
        CodeSurfaceOverlap.LiteralPrefix("*.cs").ShouldBe("");
        CodeSurfaceOverlap.LiteralPrefix("**/generated/**").ShouldBe("");
        CodeSurfaceOverlap.LiteralPrefix("src/**/web").ShouldBe("src/");
        CodeSurfaceOverlap.LiteralPrefix("src/web/Program.cs").ShouldBe("src/web/Program.cs");
        CodeSurfaceOverlap.LiteralPrefix("src/web/").ShouldBe("src/web/");
    }

    [Fact]
    public void SetsOverlap_EmptyOrMissingSurfaces_NeverOverlap()
    {
        CodeSurfaceOverlap.SetsOverlap([], ["src/**"]).ShouldBeFalse();
        CodeSurfaceOverlap.SetsOverlap(["src/**"], []).ShouldBeFalse();
        CodeSurfaceOverlap.SetsOverlap([], []).ShouldBeFalse();
    }

    [Fact]
    public void SharedGlobs_KeepsLeftOrderAndCap()
    {
        var shared = CodeSurfaceOverlap.SharedGlobs(
            ["src/api/**", "src/shared/**", "docs/**", "src/api/x.cs", "src/api/y.cs"],
            ["src/api/**", "src/shared/**", "src/api/x.cs"],
            cap: 3);

        shared.ShouldBe(["src/api/**", "src/shared/**", "src/api/x.cs"]);
    }

    [Fact]
    public void SharedGlobs_NoOverlap_ReturnsEmpty()
    {
        CodeSurfaceOverlap.SharedGlobs(["src/web/**"], ["src/api/**"], cap: 5).ShouldBeEmpty();
    }

    [Fact]
    public void Compute_InFlightPair_ReportsBothWays()
    {
        var alpha = InFlight("001-alpha", ["src/web/**"], claimed: true);
        var beta = InFlight("002-beta", ["src/web/**/*.tsx"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        overlaps[alpha.Id].Single().WithFeatureSlug.ShouldBe("002-beta");
        overlaps[beta.Id].Single().WithFeatureSlug.ShouldBe("001-alpha");
        overlaps[alpha.Id].Single().SharedGlobs.ShouldBe(["src/web/**"]);
        overlaps[beta.Id].Single().SharedGlobs.ShouldBe(["src/web/**"]);
    }

    [Fact]
    public void Compute_DisjointSurfaces_NoRows()
    {
        var alpha = InFlight("001-alpha", ["src/web/**"], claimed: true);
        var beta = InFlight("002-beta", ["src/api/**"], claimed: true);

        CodeSurfaceOverlapProjection.Compute([alpha, beta]).ShouldBeEmpty();
    }

    [Fact]
    public void Compute_DoneAndInactiveFeaturesExcluded()
    {
        var done = new CodeSurfaceFeatureRow(
            Guid.NewGuid(), "001-done", PlanningFeatureStatus.Done, null,
            CodeSurfacesJson.Serialize(["src/web/**"]));
        var draft = new CodeSurfaceFeatureRow(
            Guid.NewGuid(), "002-draft", PlanningFeatureStatus.Draft, null,
            CodeSurfacesJson.Serialize(["src/web/**"]));
        var live = InFlight("003-live", ["src/web/**"], claimed: true);
        var livePeer = InFlight("004-peer", ["src/web/**"], claimed: true);

        var overlaps = CodeSurfaceOverlapProjection.Compute([done, draft, live, livePeer]);

        overlaps.Count.ShouldBe(2);
        overlaps[live.Id].Single().WithFeatureSlug.ShouldBe("004-peer");
        overlaps[livePeer.Id].Single().WithFeatureSlug.ShouldBe("003-live");
    }

    [Fact]
    public void Compute_InProgressOrReviewStatusesCountAsInFlightWithoutClaim()
    {
        var alpha = InFlight(
            "001-alpha", ["src/**"], claimed: false, status: PlanningFeatureStatus.InProgress);
        var beta = InFlight(
            "002-beta", ["src/api/**"], claimed: false, status: PlanningFeatureStatus.Review);

        var overlaps = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        overlaps.Count.ShouldBe(2);
    }

    [Fact]
    public void Compute_FeaturesWithoutSurfaces_StayOutOfPairs()
    {
        var noSurfaces = InFlight("001-alpha", null, claimed: true);
        var emptySurfaces = InFlight("002-beta", [], claimed: true);
        var lonely = InFlight("003-gamma", ["src/api/**"], claimed: true);

        CodeSurfaceOverlapProjection.Compute([noSurfaces, emptySurfaces, lonely]).ShouldBeEmpty();
    }

    [Fact]
    public void Compute_OrdersRowsByPartnerSlug_AndCapsAtFive()
    {
        var hub = InFlight("001-hub", ["src/**"], claimed: true);
        var peers = Enumerable.Range(2, 9)
            .Reverse()
            .Select(i => InFlight($"{i:00}-peer", ["src/**"], claimed: true))
            .ToArray();

        var overlaps = CodeSurfaceOverlapProjection.Compute([hub, .. peers]);

        var partners = overlaps[hub.Id].Select(r => r.WithFeatureSlug).ToArray();
        partners.Length.ShouldBe(CodeSurfaceOverlapProjection.MaxRowsPerFeature);
        partners.ShouldBe(partners.OrderBy(p => p, StringComparer.Ordinal).ToArray());
        partners.ShouldContain("02-peer");
        partners.ShouldNotContain("10-peer");
    }

    [Fact]
    public void Compute_DeterministicAcrossRuns()
    {
        var alpha = InFlight("001-alpha", ["a/**", "b/**"], claimed: true);
        var beta = InFlight("002-beta", ["b/**", "a/**"], claimed: true);

        var first = CodeSurfaceOverlapProjection.Compute([alpha, beta]);
        var second = CodeSurfaceOverlapProjection.Compute([alpha, beta]);

        first[alpha.Id]
            .Select(r => (r.WithFeatureSlug, Globs: string.Join("|", r.SharedGlobs)))
            .ShouldBe(second[alpha.Id].Select(r => (r.WithFeatureSlug, Globs: string.Join("|", r.SharedGlobs))));
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
