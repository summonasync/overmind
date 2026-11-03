using System.Threading.Channels;
using Overmind.Core;

namespace Overmind.Host.WaveChat;

public sealed class WaveStreamSubscription(ChannelReader<WaveMessage> messages, Action detach) : IAsyncDisposable
{
    public ChannelReader<WaveMessage> Messages { get; } = messages;

    public ValueTask DisposeAsync()
    {
        detach();
        return ValueTask.CompletedTask;
    }
}
