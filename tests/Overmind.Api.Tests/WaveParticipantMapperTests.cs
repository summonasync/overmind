using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WaveParticipantMapperTests
{
    [Fact]
    public void ToResponse_OwnerAndHolders_RowsCarryTheirRoles()
    {
        var lanes = new List<LaneRow>
        {
            Lane("backend", new LaneHolder("Van", "claude", AgentSessionLiveness.Alive, AgentSession.MarshalRole)),
            Lane("web", new LaneHolder("Dan", "qwen", AgentSessionLiveness.Alive, AgentSession.DefaultRole)),
        };

        var roster = WaveParticipantMapper.ToResponse(lanes, [], "owner");

        roster.Items.Length.ShouldBe(3);
        roster.Items[0].ShouldBe(new WaveParticipantResponse(
            WaveParticipantResponse.OwnerKind, null, "owner", null, null, WaveParticipantResponse.OwnerRole));
        roster.Items[1].ShouldBe(new WaveParticipantResponse(
            WaveParticipantResponse.AgentKind, "backend", "Van", "claude", AgentSessionLiveness.Alive, "marshal"));
        roster.Items[2].ShouldBe(new WaveParticipantResponse(
            WaveParticipantResponse.AgentKind, "web", "Dan", "qwen", AgentSessionLiveness.Alive, "worker"));
    }

    [Fact]
    public void ToResponse_UnclaimedLanes_RendersOwnerOnly()
    {
        var lanes = new List<LaneRow> { Lane("backend", null) };

        var roster = WaveParticipantMapper.ToResponse(lanes, [], "owner");

        roster.Items.ShouldBe(new WaveParticipantResponse[]
        {
            new(WaveParticipantResponse.OwnerKind, null, "owner", null, null, WaveParticipantResponse.OwnerRole)
        });
    }

    [Fact]
    public void ToResponse_MultipleLanes_KeepsOwnerFirstThenLaneOrder()
    {
        var lanes = new List<LaneRow>
        {
            Lane("web", new LaneHolder("Dan", "qwen", AgentSessionLiveness.Stale, AgentSession.DefaultRole)),
            Lane("backend", new LaneHolder("Van", "claude", AgentSessionLiveness.Alive, AgentSession.MarshalRole)),
        };

        var roster = WaveParticipantMapper.ToResponse(lanes, [], "owner");

        roster.Items.Select(p => p.Lane).ShouldBe([null, "backend", "web"]);
        roster.Items[1].ClaimState.ShouldBe(AgentSessionLiveness.Alive);
        roster.Items[1].Role.ShouldBe("marshal");
        roster.Items[2].ClaimState.ShouldBe(AgentSessionLiveness.Stale);
        roster.Items[2].Role.ShouldBe("worker");
    }

    [Fact]
    public void ToResponse_HolderLastSeenAt_FlowsAsUtcInstantAndOwnerStaysNull()
    {
        var seenAt = new DateTime(2026, 10, 25, 9, 30, 0, DateTimeKind.Unspecified);
        var lanes = new List<LaneRow>
        {
            Lane("backend", new LaneHolder("Van", "claude", AgentSessionLiveness.Alive, AgentSession.DefaultRole, seenAt)),
        };

        var roster = WaveParticipantMapper.ToResponse(lanes, [], "owner");

        roster.Items[0].LastSeenAt.ShouldBeNull();
        roster.Items[1].LastSeenAt.ShouldBe(new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToResponse_ClaimlessSessions_LanelessRowsAfterHoldersSortedByAlias()
    {
        var seenAt = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);
        var lanes = new List<LaneRow>
        {
            Lane("backend", new LaneHolder("Van", "claude", AgentSessionLiveness.Alive, AgentSession.MarshalRole)),
        };
        var sessions = new List<WaveSessionPresence>
        {
            new("Zed", "kimi", AgentSession.DefaultRole, seenAt.AddMinutes(5)),
            new("Dan", "qwen", AgentSession.MarshalRole, seenAt),
        };

        var roster = WaveParticipantMapper.ToResponse(lanes, sessions, "owner");

        roster.Items.ShouldBe(new WaveParticipantResponse[]
        {
            new(WaveParticipantResponse.OwnerKind, null, "owner", null, null, WaveParticipantResponse.OwnerRole),
            new(WaveParticipantResponse.AgentKind, "backend", "Van", "claude", AgentSessionLiveness.Alive,
                AgentSession.MarshalRole, null),
            new(WaveParticipantResponse.AgentKind, null, "Dan", "qwen", null, AgentSession.MarshalRole,
                new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero)),
            new(WaveParticipantResponse.AgentKind, null, "Zed", "kimi", null, AgentSession.DefaultRole,
                new DateTimeOffset(2026, 10, 31, 8, 5, 0, TimeSpan.Zero)),
        });
    }

    private static LaneRow Lane(string name, LaneHolder? holder) =>
        new(name, null, null, null, null, [], 1, holder, []);
}
