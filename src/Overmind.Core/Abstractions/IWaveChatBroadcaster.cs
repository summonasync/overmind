namespace Overmind.Core;

public interface IWaveChatBroadcaster
{
    void Publish(Guid projectId, string room, WaveMessage message);
}
