using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class PlanningArtifactTests
{
    private static readonly Guid FeatureId = Guid.NewGuid();

    [Theory]
    [InlineData(PlanningArtifactKind.Spec, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Plan, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Result, PlanningArtifactStatus.Draft)]
    [InlineData(PlanningArtifactKind.Task, PlanningArtifactStatus.Todo)]
    public void Create_SetsInitialStatusPerKind(PlanningArtifactKind kind, PlanningArtifactStatus expectedStatus)
    {
        var artifact = PlanningArtifact.Create(FeatureId, kind, "body content");

        artifact.Status.ShouldBe(expectedStatus);
        artifact.Version.ShouldBe(1);
        artifact.Kind.ShouldBe(kind);
        artifact.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public void AllowedStatuses_SpecPlanResult_OnlyDraftAndValidated()
    {
        var allowed = PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Spec);

        allowed.ShouldBe([PlanningArtifactStatus.Draft, PlanningArtifactStatus.Validated]);
        PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Plan).ShouldBe(allowed);
        PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Result).ShouldBe(allowed);
    }

    [Fact]
    public void AllowedStatuses_Task_ExcludesValidated()
    {
        var allowed = PlanningArtifact.AllowedStatuses(PlanningArtifactKind.Task);

        allowed.ShouldContain(PlanningArtifactStatus.Todo);
        allowed.ShouldContain(PlanningArtifactStatus.InProgress);
        allowed.ShouldContain(PlanningArtifactStatus.Review);
        allowed.ShouldContain(PlanningArtifactStatus.Done);
        allowed.ShouldContain(PlanningArtifactStatus.Blocked);
        allowed.ShouldNotContain(PlanningArtifactStatus.Validated);
        allowed.ShouldNotContain(PlanningArtifactStatus.Draft);
    }

    [Fact]
    public void SetStatus_TaskCannotBecomeValidated()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Validated));
    }

    [Fact]
    public void SetStatus_SpecCannotBecomeDone()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Done));
    }

    [Fact]
    public void Validate_SpecOrPlan_FreezesAndSetsFrozenAt()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        var before = DateTime.UtcNow;

        artifact.Validate();

        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.FrozenAt.ShouldNotBeNull();
        artifact.FrozenAt.Value.ShouldBeGreaterThanOrEqualTo(before);
        artifact.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Task_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<InvalidOperationException>(() => artifact.Validate());
    }

    [Fact]
    public void UpdateBody_FrozenWithoutNote_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "original");
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() =>
            artifact.UpdateBody("new body"));
    }

    [Fact]
    public void UpdateBody_FrozenWithNote_ResetsToDraft_ClearsFrozenAt_AppendsLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "original");
        artifact.Validate();
        artifact.IsFrozen.ShouldBeTrue();

        artifact.UpdateBody("new body", "reason for change");

        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.FrozenAt.ShouldBeNull();
        artifact.IsFrozen.ShouldBeFalse();
        artifact.Body.ShouldContain("reason for change");
        artifact.Body.ShouldContain("<log>");
        artifact.Versions.Count.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("original");
        artifact.Version.ShouldBe(2);
    }

    [Fact]
    public void UpdateBody_NotFrozen_SnapshotsVersion()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "v1");

        artifact.UpdateBody("v2");

        artifact.Body.ShouldBe("v2");
        artifact.Version.ShouldBe(2);
        artifact.Versions.Count.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("v1");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public void SetStatus_OffValidated_WithNote_Unfreezes()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        artifact.Validate();
        artifact.IsFrozen.ShouldBeTrue();

        artifact.SetStatus(PlanningArtifactStatus.Draft, "unfreezing for rework");

        artifact.FrozenAt.ShouldBeNull();
        artifact.Status.ShouldBe(PlanningArtifactStatus.Draft);
        artifact.Body.ShouldContain("unfreezing for rework");
    }

    [Fact]
    public void SetStatus_OffValidated_WithoutNote_Throws()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Spec, "body");
        artifact.Validate();

        Should.Throw<InvalidOperationException>(() =>
            artifact.SetStatus(PlanningArtifactStatus.Draft));
    }

    [Fact]
    public void AppendLog_DoesNotBumpVersion_CreatesNoVersionRow()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");
        var versionBefore = artifact.Version;

        artifact.AppendLog("some note");

        artifact.Version.ShouldBe(versionBefore);
        artifact.Versions.ShouldBeEmpty();
        artifact.Body.ShouldContain("some note");
        artifact.Body.ShouldContain("<log>");
    }

    [Fact]
    public void SetStatus_WithNote_AppendsToLog()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        artifact.SetStatus(PlanningArtifactStatus.InProgress, "starting work");

        artifact.Body.ShouldContain("starting work");
        artifact.Body.ShouldContain("<log>");
    }

    [Fact]
    public void UpdateBody_NotFrozen_WithNote_AppendsNote()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "v1");

        artifact.UpdateBody("v2", "improved logic");

        artifact.Body.ShouldContain("improved logic");
        artifact.Body.ShouldContain("<log>");
        artifact.Status.ShouldBe(PlanningArtifactStatus.Todo);
    }

    [Fact]
    public void UpdateBody_ThreeConsecutiveCalls_VersionRowsTrackBodies()
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body-v1");

        artifact.UpdateBody("body-v2");
        artifact.UpdateBody("body-v3");
        artifact.UpdateBody("body-v4");

        artifact.Version.ShouldBe(4);
        artifact.Body.ShouldBe("body-v4");
        artifact.Versions.Count.ShouldBe(3);
        artifact.Versions[0].Version.ShouldBe(1);
        artifact.Versions[0].Body.ShouldBe("body-v1");
        artifact.Versions[1].Version.ShouldBe(2);
        artifact.Versions[1].Body.ShouldBe("body-v2");
        artifact.Versions[2].Version.ShouldBe(3);
        artifact.Versions[2].Body.ShouldBe("body-v3");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AppendLog_EmptyNote_Throws(string note)
    {
        var artifact = PlanningArtifact.Create(FeatureId, PlanningArtifactKind.Task, "body");

        Should.Throw<ArgumentException>(() => artifact.AppendLog(note));
    }

    [Fact]
    public void Restore_RoundTripsAllFields()
    {
        var featureId = Guid.NewGuid();
        var frozenAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var artifact = PlanningArtifact.Restore(
            featureId,
            PlanningArtifactKind.Spec,
            PlanningArtifactStatus.Validated,
            PlanningReviewLevel.Hard,
            "imported body",
            order: 3,
            frozenAt: frozenAt,
            version: 5,
            createdAt: createdAt,
            updatedAt: updatedAt);

        artifact.FeatureId.ShouldBe(featureId);
        artifact.Kind.ShouldBe(PlanningArtifactKind.Spec);
        artifact.Status.ShouldBe(PlanningArtifactStatus.Validated);
        artifact.Review.ShouldBe(PlanningReviewLevel.Hard);
        artifact.Body.ShouldBe("imported body");
        artifact.Order.ShouldBe(3);
        artifact.FrozenAt.ShouldBe(frozenAt);
        artifact.Version.ShouldBe(5);
        artifact.CreatedAt.ShouldBe(createdAt);
        artifact.UpdatedAt.ShouldBe(updatedAt);
        artifact.IsFrozen.ShouldBeTrue();
        artifact.Versions.ShouldBeEmpty();
    }

    [Fact]
    public void Restore_InvalidStatus_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Done,
                PlanningReviewLevel.Light,
                "body",
                0,
                null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }

    [Fact]
    public void Restore_ValidatedWithoutFrozenAt_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Validated,
                PlanningReviewLevel.Light,
                "body",
                0,
                frozenAt: null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }

    [Fact]
    public void Restore_DraftWithFrozenAt_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            PlanningArtifact.Restore(
                Guid.NewGuid(),
                PlanningArtifactKind.Spec,
                PlanningArtifactStatus.Draft,
                PlanningReviewLevel.Light,
                "body",
                0,
                frozenAt: DateTime.UtcNow,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow));
    }
}
