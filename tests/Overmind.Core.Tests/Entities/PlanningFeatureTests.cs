using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class PlanningFeatureTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public void Create_SetsDefaults()
    {
        var before = DateTime.UtcNow;

        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth flow", "body text");

        feature.Id.ShouldNotBe(Guid.Empty);
        feature.ProjectId.ShouldBe(ProjectId);
        feature.Slug.ShouldBe("001-auth");
        feature.Title.ShouldBe("Auth flow");
        feature.Body.ShouldBe("body text");
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        feature.Priority.ShouldBe(FeaturePriority.P2);
        feature.Version.ShouldBe(1);
        feature.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        feature.UpdatedAt.ShouldBe(feature.CreatedAt);
        feature.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public void Create_NullBody_StoredAsEmpty()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", null!);

        feature.Body.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_ExplicitPriority_Stored()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body", FeaturePriority.P1);

        feature.Priority.ShouldBe(FeaturePriority.P1);
    }

    [Fact]
    public void SetPriority_ChangesPriorityTouchesUpdatedAtKeepsVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;

        feature.SetPriority(FeaturePriority.P0);

        feature.Priority.ShouldBe(FeaturePriority.P0);
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
        feature.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptySlug_Throws(string slug)
    {
        Should.Throw<ArgumentException>(() =>
            PlanningFeature.Create(ProjectId, slug, "title", "body"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyTitle_Throws(string title)
    {
        Should.Throw<ArgumentException>(() =>
            PlanningFeature.Create(ProjectId, "slug", title, "body"));
    }

    [Fact]
    public void Update_InDraft_DoesNotBumpVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var createdAt = feature.CreatedAt;

        feature.Update("new title", "new body");

        feature.Title.ShouldBe("new title");
        feature.Body.ShouldBe("new body");
        feature.Version.ShouldBe(1);
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(createdAt);
    }

    [Fact]
    public void Update_AfterLeavingDraft_BumpsVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetStatus(PlanningFeatureStatus.Ready);

        feature.Update("new title", "new body");

        feature.Version.ShouldBe(2);
    }

    [Fact]
    public void Update_AlwaysTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;

        feature.Update("changed", "body");

        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void SetStatus_ChangesStatus()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.SetStatus(PlanningFeatureStatus.InProgress);

        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
    }

    [Fact]
    public void Update_NullTitle_NullBody_ChangesNothingButTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var originalTitle = feature.Title;
        var originalBody = feature.Body;
        var originalVersion = feature.Version;

        feature.Update(null, null);

        feature.Title.ShouldBe(originalTitle);
        feature.Body.ShouldBe(originalBody);
        feature.Version.ShouldBe(originalVersion);
    }

    [Fact]
    public void Update_TitleOnly_KeepsBody()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "original body");

        feature.Update("new title", null);

        feature.Title.ShouldBe("new title");
        feature.Body.ShouldBe("original body");
    }

    [Fact]
    public void Update_BodyStoredVerbatim_PreservesWhitespace()
    {
        var verbatimBody = "  leading\ntrailing\n\n";
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "initial");

        feature.Update(null, verbatimBody);

        feature.Body.ShouldBe(verbatimBody);
    }

    [Fact]
    public void Restore_RoundTripsAllFields()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var feature = PlanningFeature.Restore(
            ProjectId,
            "001-auth",
            "Auth flow",
            "imported body",
            PlanningFeatureStatus.Ready,
            version: 3,
            createdAt: createdAt,
            updatedAt: updatedAt,
            isCurrent: false);

        feature.ProjectId.ShouldBe(ProjectId);
        feature.Slug.ShouldBe("001-auth");
        feature.Title.ShouldBe("Auth flow");
        feature.Body.ShouldBe("imported body");
        feature.Status.ShouldBe(PlanningFeatureStatus.Ready);
        feature.IsCurrent.ShouldBeFalse();
        feature.Priority.ShouldBe(FeaturePriority.P2);
        feature.Version.ShouldBe(3);
        feature.CreatedAt.ShouldBe(createdAt);
        feature.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void Restore_PinnedFeature_RoundTripsCurrentFlag()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var feature = PlanningFeature.Restore(
            ProjectId,
            "001-auth",
            "Auth flow",
            "imported body",
            PlanningFeatureStatus.InProgress,
            version: 2,
            createdAt: createdAt,
            updatedAt: createdAt,
            isCurrent: true);

        feature.IsCurrent.ShouldBeTrue();
        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
    }

    [Fact]
    public void SetCurrent_Pin_SetsFlagAndTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;

        feature.SetCurrent(true);

        feature.IsCurrent.ShouldBeTrue();
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void SetCurrent_Unpin_ClearsFlagAndLeavesStatusUntouched()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCurrent(true);

        feature.SetCurrent(false);

        feature.IsCurrent.ShouldBeFalse();
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
    }
}
