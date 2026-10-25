using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record PostWaveMessageRequest(string Text);

public sealed record PostWaveMessageHumanRequest(string AuthorAlias, string Text);

public sealed record WaveMessageResponse(
    Guid Id,
    string Room,
    string AuthorAlias,
    string Text,
    IReadOnlyList<string> Mentions,
    DateTime CreatedAt,
    string AuthorRole = AgentSession.DefaultRole);

public sealed record WaveUnreadResponse(WaveMessageResponse[] Items, DateTime WatermarkAt);

public sealed record WaveHistoryResponse(WaveMessageResponse[] Items);

public sealed record WaveRoomResponse(string Room, DateTime? LastActivityAt);

public sealed record WaveRoomsResponse(WaveRoomResponse[] Items);

public sealed record WaveParticipantResponse(
    string Kind,
    string? Lane,
    string Alias,
    string? Vendor,
    AgentSessionLiveness? ClaimState,
    string Role = AgentSession.DefaultRole)
{
    public const string OwnerKind = "owner";
    public const string AgentKind = "agent";
    public const string OwnerRole = WaveMessage.OwnerRole;
}

public sealed record WaveParticipantsResponse(WaveParticipantResponse[] Items);
