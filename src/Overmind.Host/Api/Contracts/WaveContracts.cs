namespace Overmind.Host.Api.Contracts;

public sealed record PostWaveMessageRequest(string Text);

public sealed record PostWaveMessageHumanRequest(string AuthorAlias, string Text);

public sealed record WaveMessageResponse(
    Guid Id,
    string Room,
    string AuthorAlias,
    string Text,
    IReadOnlyList<string> Mentions,
    DateTime CreatedAt);

public sealed record WaveUnreadResponse(WaveMessageResponse[] Items, DateTime WatermarkAt);

public sealed record WaveRoomResponse(string Room, DateTime? LastActivityAt);

public sealed record WaveRoomsResponse(WaveRoomResponse[] Items);
