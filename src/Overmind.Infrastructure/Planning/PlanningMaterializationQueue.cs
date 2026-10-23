using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationQueue
{
    private readonly Channel<Guid> _channel;
    private readonly object _pendingGate = new();
    private readonly Dictionary<Guid, int> _pending = [];

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

    public int PendingCountFor(Guid projectId)
    {
        lock (_pendingGate)
        {
            return _pending.GetValueOrDefault(projectId);
        }
    }

    public bool TryEnqueue(Guid projectId)
    {
        if (!_channel.Writer.TryWrite(projectId))
        {
            return false;
        }

        lock (_pendingGate)
        {
            _pending[projectId] = _pending.GetValueOrDefault(projectId) + 1;
        }

        return true;
    }

    public bool TryDequeue(out Guid projectId)
    {
        if (!_channel.Reader.TryRead(out projectId))
        {
            return false;
        }

        Decrement(projectId);
        return true;
    }

    public async IAsyncEnumerable<Guid> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var projectId in _channel.Reader.ReadAllAsync(ct))
        {
            Decrement(projectId);
            yield return projectId;
        }
    }

    private void Decrement(Guid projectId)
    {
        lock (_pendingGate)
        {
            var remaining = _pending.GetValueOrDefault(projectId) - 1;
            if (remaining > 0)
            {
                _pending[projectId] = remaining;
            }
            else
            {
                _pending.Remove(projectId);
            }
        }
    }
}
