using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api.Mapping;

public static class WaveParticipantMapper
{
    public static WaveParticipantsResponse ToResponse(IReadOnlyList<LaneRow> lanes, string ownerAlias) =>
        new(
        [
            new WaveParticipantResponse(
                WaveParticipantResponse.OwnerKind, null, ownerAlias, null, null),
            .. lanes
                .Where(l => l.Holder is not null)
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .Select(l => new WaveParticipantResponse(
                    WaveParticipantResponse.AgentKind,
                    l.Name,
                    l.Holder!.Alias,
                    l.Holder.Vendor,
                    l.Holder.ClaimState))
        ]);
}
