using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Overmind.Core;

namespace Overmind.Host.AgentChat;

public sealed class ConversationTurn(Guid conversationId)
{
    private readonly object _gate = new();
    private readonly List<Channel<ConversationStreamEvent>> _subscribers = [];
    private readonly CancellationTokenSource _lifecycle = new();
    private readonly StringBuilder _reply = new();
    private ConversationStreamEvent? _terminal;

    public Guid ConversationId { get; } = conversationId;

    public CancellationToken CancellationToken => _lifecycle.Token;

    public bool HasReply
    {
        get
        {
            lock (_gate)
            {
                return _reply.Length > 0;
            }
        }
    }

    public string ReplyText
    {
        get
        {
            lock (_gate)
            {
                return _reply.ToString();
            }
        }
    }

    public void RequestStop()
    {
        try
        {
            _lifecycle.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void AppendReply(string text)
    {
        lock (_gate)
        {
            _reply.Append(text);
        }
    }

    public void Publish(ConversationStreamEvent streamEvent)
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(streamEvent);
            }
        }
    }

    public void Complete(long? tokensUsed, Guid? messageId) =>
        End(ConversationStreamEvent.Completed(tokensUsed, messageId));

    public void Fail(string error, string? code) => End(ConversationStreamEvent.Failed(error, code));

    public void Abort() => End(ConversationStreamEvent.Aborted());

    public void DisposeLifetime() => _lifecycle.Dispose();

    public IAsyncEnumerable<ConversationStreamEvent> Subscribe(CancellationToken ct = default)
    {
        var channel = RegisterSubscriber();
        return ReadCore(channel, ct);
    }

    private Channel<ConversationStreamEvent> RegisterSubscriber()
    {
        var channel = Channel.CreateUnbounded<ConversationStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        lock (_gate)
        {
            if (_reply.Length > 0)
            {
                channel.Writer.TryWrite(ConversationStreamEvent.Delta(_reply.ToString()));
            }

            if (_terminal is { } terminal)
            {
                channel.Writer.TryWrite(terminal);
                channel.Writer.TryComplete();
            }
            else
            {
                _subscribers.Add(channel);
            }
        }

        return channel;
    }

    private async IAsyncEnumerable<ConversationStreamEvent> ReadCore(
        Channel<ConversationStreamEvent> channel,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var streamEvent in channel.Reader.ReadAllAsync(ct))
            {
                yield return streamEvent;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    private void End(ConversationStreamEvent terminal)
    {
        lock (_gate)
        {
            if (_terminal is not null)
            {
                return;
            }

            _terminal = terminal;
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(terminal);
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }
}
