using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class FeatureNumberGuardTests
{
    [Theory]
    [InlineData("075-sprint", "75-other")]
    [InlineData("75-other", "075-sprint")]
    public void FindCollision_NumberEquivalentSlugs_Collides(string existingSlug, string requestedSlug)
    {
        var collision = FeatureNumberGuard.FindCollision(Entries((existingSlug, PlanningFeatureStatus.Draft)), requestedSlug);

        collision.ShouldNotBeNull();
        collision.CollidingSlug.ShouldBe(existingSlug);
        collision.CollidingStatus.ShouldBe(PlanningFeatureStatus.Draft);
    }

    [Fact]
    public void FindCollision_DistinctNumbers_ReturnsNull()
    {
        var features = Entries(("075-a", PlanningFeatureStatus.Done), ("76-b", PlanningFeatureStatus.Draft));

        FeatureNumberGuard.FindCollision(features, "7-c").ShouldBeNull();
    }

    [Fact]
    public void FindCollision_NumberlessRequest_ReturnsNull()
    {
        var features = Entries(("007-x", PlanningFeatureStatus.Draft), ("120-y", PlanningFeatureStatus.Ready));

        FeatureNumberGuard.FindCollision(features, "notes").ShouldBeNull();
    }

    [Fact]
    public void FindCollision_NumberlessResidents_NeverCollide()
    {
        var features = Entries(("notes", PlanningFeatureStatus.Draft), ("docs", PlanningFeatureStatus.Ready));

        FeatureNumberGuard.FindCollision(features, "more-notes").ShouldBeNull();
    }

    [Fact]
    public void FindCollision_Collision_SuggestsMaxPlusOne()
    {
        var features = Entries(
            ("007-x", PlanningFeatureStatus.Draft),
            ("numberless", PlanningFeatureStatus.Ready),
            ("120-y", PlanningFeatureStatus.Done));

        var collision = FeatureNumberGuard.FindCollision(features, "7-z");

        collision.ShouldNotBeNull();
        collision.SuggestedNumber.ShouldBe(121);
    }

    [Fact]
    public void FindCollision_MaxHeldNumber_SuggestionStaysBounded()
    {
        var features = Entries(
            ("007-x", PlanningFeatureStatus.Draft),
            ("2147483647-y", PlanningFeatureStatus.Done));

        var collision = FeatureNumberGuard.FindCollision(features, "7-z");

        collision.ShouldNotBeNull();
        collision.SuggestedNumber.ShouldBe(int.MaxValue);
    }

    private static IReadOnlyList<FeatureNumberEntry> Entries(params (string Slug, PlanningFeatureStatus Status)[] items) =>
        [.. items.Select(i => new FeatureNumberEntry(i.Slug, i.Status))];
}
