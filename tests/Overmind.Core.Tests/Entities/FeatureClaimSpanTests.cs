using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class FeatureClaimSpanTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid FeatureId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void Open_SetsFieldsAndTrimsIdentity()
    {
        var claimedAt = DateTime.UtcNow.AddMinutes(-5);

        var span = FeatureClaimSpan.Open(ProjectId, FeatureId, "  Dan  ", " qwen ", SessionId, claimedAt, true);

        span.Id.ShouldNotBe(Guid.Empty);
        span.ProjectId.ShouldBe(ProjectId);
        span.FeatureId.ShouldBe(FeatureId);
        span.Alias.ShouldBe("Dan");
        span.Vendor.ShouldBe("qwen");
        span.SessionId.ShouldBe(SessionId);
        span.ClaimedAt.ShouldBe(claimedAt);
        span.ReleasedAt.ShouldBeNull();
        span.ByHuman.ShouldBeTrue();
    }

    [Fact]
    public void Close_StampsReleasedAt()
    {
        var span = FeatureClaimSpan.Open(ProjectId, FeatureId, "Dan", "qwen", SessionId, DateTime.UtcNow);
        var releasedAt = DateTime.UtcNow.AddHours(1);

        span.Close(releasedAt);

        span.ReleasedAt.ShouldBe(releasedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Open_EmptyAlias_Throws(string? alias)
    {
        Should.Throw<ArgumentException>(
            () => FeatureClaimSpan.Open(ProjectId, FeatureId, alias!, "qwen", SessionId, DateTime.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Open_EmptyVendor_Throws(string? vendor)
    {
        Should.Throw<ArgumentException>(
            () => FeatureClaimSpan.Open(ProjectId, FeatureId, "Dan", vendor!, SessionId, DateTime.UtcNow));
    }

    [Fact]
    public void Open_IdentityOverTheCap_Throws()
    {
        var longIdentity = new string('x', AgentSession.MaxIdentityLength + 1);

        Should.Throw<ArgumentException>(
            () => FeatureClaimSpan.Open(ProjectId, FeatureId, longIdentity, "qwen", SessionId, DateTime.UtcNow));
        Should.Throw<ArgumentException>(
            () => FeatureClaimSpan.Open(ProjectId, FeatureId, "Dan", longIdentity, SessionId, DateTime.UtcNow));
    }
}
