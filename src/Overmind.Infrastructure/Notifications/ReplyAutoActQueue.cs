using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Overmind.Infrastructure.Notifications;

public sealed class ReplyAutoActQueue(int capacity = 64)
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    public int PendingCount => _channel.Reader.Count;

    public bool TryEnqueue(Guid notificationId) => _channel.Writer.TryWrite(notificationId);

    public bool TryDequeue(out Guid notificationId) => _channel.Reader.TryRead(out notificationId);

    public async IAsyncEnumerable<Guid> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var notificationId in _channel.Reader.ReadAllAsync(ct))
        {
            yield return notificationId;
        }
    }
}
