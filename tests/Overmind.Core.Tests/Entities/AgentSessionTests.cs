using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class AgentSessionTests
{
    [Fact]
    public void Create_SetsDefaultsAndTrimsIdentity()
    {
        var before = DateTime.UtcNow;

        var session = AgentSession.Create("  Dan  ", "qwen");

        session.Id.ShouldNotBe(Guid.Empty);
        session.Alias.ShouldBe("Dan");
        session.Vendor.ShouldBe("qwen");
        session.StartedAt.ShouldBeGreaterThanOrEqualTo(before);
        session.LastSeenAt.ShouldBe(session.StartedAt);
        session.EndedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyAlias_Throws(string? alias)
    {
        Should.Throw<ArgumentException>(() => AgentSession.Create(alias!, "qwen"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyVendor_Throws(string? vendor)
    {
        Should.Throw<ArgumentException>(() => AgentSession.Create("Dan", vendor!));
    }

    [Theory]
    [InlineData(33, 5)]
    [InlineData(5, 33)]
    public void Create_IdentityOverCap_Throws(int aliasLength, int vendorLength)
    {
        Should.Throw<ArgumentException>(() =>
            AgentSession.Create(new string('a', aliasLength), new string('b', vendorLength)));
    }

    [Fact]
    public void Touch_UpdatesLastSeenAt()
    {
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(-1));
        var before = DateTime.UtcNow;

        session.Touch();

        session.LastSeenAt.ShouldBeGreaterThanOrEqualTo(before);
        session.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void Touch_EndedSession_KeepsLastSeenAt()
    {
        var endedAt = DateTime.UtcNow.AddMinutes(-5);
        var lastSeenAt = DateTime.UtcNow.AddMinutes(-10);
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddHours(-1), lastSeenAt, endedAt);

        session.Touch();

        session.LastSeenAt.ShouldBe(lastSeenAt);
    }

    [Fact]
    public void End_SetsEndedAtOnce()
    {
        var session = AgentSession.Create("Dan", "qwen");

        session.End();
        var endedAt = session.EndedAt;

        session.End();

        session.EndedAt.ShouldBe(endedAt);
        endedAt.ShouldNotBeNull();
        endedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Restore_RoundTripsAllFields()
    {
        var id = Guid.NewGuid();
        var startedAt = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var lastSeenAt = new DateTime(2026, 9, 30, 9, 30, 0, DateTimeKind.Utc);
        var endedAt = new DateTime(2026, 9, 30, 9, 31, 0, DateTimeKind.Utc);

        var session = AgentSession.Restore(id, "Dan", "qwen", startedAt, lastSeenAt, endedAt);

        session.Id.ShouldBe(id);
        session.Alias.ShouldBe("Dan");
        session.Vendor.ShouldBe("qwen");
        session.StartedAt.ShouldBe(startedAt);
        session.LastSeenAt.ShouldBe(lastSeenAt);
        session.EndedAt.ShouldBe(endedAt);
    }

    [Fact]
    public void Liveness_EndedSession_ReturnsEndedRegardlessOfAge()
    {
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow.AddMinutes(1));

        session.Liveness(DateTime.UtcNow, 15).ShouldBe(AgentSessionLiveness.Ended);
        session.Liveness(DateTime.UtcNow, 0).ShouldBe(AgentSessionLiveness.Ended);
    }

    [Fact]
    public void Liveness_RecentLastSeen_ReturnsAlive()
    {
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-14));

        session.Liveness(DateTime.UtcNow, 15).ShouldBe(AgentSessionLiveness.Alive);
    }

    [Fact]
    public void Liveness_LastSeenOlderThanThreshold_ReturnsStale()
    {
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-16));

        session.Liveness(DateTime.UtcNow, 15).ShouldBe(AgentSessionLiveness.Stale);
    }

    [Fact]
    public void Liveness_ZeroThreshold_DisablesStale()
    {
        var session = AgentSession.Restore(Guid.NewGuid(), "Dan", "qwen",
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddHours(-5));

        session.Liveness(DateTime.UtcNow, 0).ShouldBe(AgentSessionLiveness.Alive);
    }
}
