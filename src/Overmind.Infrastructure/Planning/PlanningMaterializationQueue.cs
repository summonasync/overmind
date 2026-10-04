using System.Threading.Channels;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationQueue
{
    private readonly Channel<Guid> _channel;

    public PlanningMaterializationQueue(int capacity = 64)
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<Guid> Reader => _channel.Reader;

    public int PendingCount => _channel.Reader.Count;

    public bool TryEnqueue(Guid projectId) => _channel.Writer.TryWrite(projectId);
}
