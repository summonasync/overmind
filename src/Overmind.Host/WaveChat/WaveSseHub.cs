using System.Threading.Channels;
using Overmind.Core;

namespace Overmind.Host.WaveChat;

public sealed class WaveSseHub : IWaveChatBroadcaster
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<Channel<WaveMessage>>> _topics = new();

    public void Publish(Guid projectId, string room, WaveMessage message)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(projectId, out var subscribers))
            {
                return;
            }

            subscribers.RemoveAll(subscriber => !subscriber.Writer.TryWrite(message));
        }
    }

    public WaveStreamSubscription Subscribe(Guid projectId)
    {
        var channel = Channel.CreateUnbounded<WaveMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        lock (_gate)
        {
            if (!_topics.TryGetValue(projectId, out var subscribers))
            {
                subscribers = [];
                _topics[projectId] = subscribers;
            }

            subscribers.Add(channel);
        }

        return new WaveStreamSubscription(channel.Reader, () => Detach(projectId, channel));
    }

    public int CountSubscribers(Guid projectId)
    {
        lock (_gate)
        {
            return _topics.TryGetValue(projectId, out var subscribers) ? subscribers.Count : 0;
        }
    }

    private void Detach(Guid projectId, Channel<WaveMessage> channel)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(projectId, out var subscribers))
            {
                return;
            }

            subscribers.Remove(channel);
            if (subscribers.Count == 0)
            {
                _topics.Remove(projectId);
            }
        }
    }
}
