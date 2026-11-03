using Overmind.Core;

namespace Overmind.Infrastructure.Tests;

public sealed record PublishedWaveMessage(Guid ProjectId, string Room, WaveMessage Message);

public sealed class StubWaveChatBroadcaster : IWaveChatBroadcaster
{
    public List<PublishedWaveMessage> Published { get; } = [];

    public void Publish(Guid projectId, string room, WaveMessage message) =>
        Published.Add(new PublishedWaveMessage(projectId, room, message));
}
