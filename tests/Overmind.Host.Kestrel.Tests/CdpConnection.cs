using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Overmind.Host.Kestrel.Tests;

public sealed class CdpConnection : IAsyncDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private readonly ClientWebSocket _socket;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentQueue<CdpEvent> _events = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private long _nextMessageId;
    private long _eventSequence;
    private Task _readerTask = Task.CompletedTask;

    private CdpConnection(ClientWebSocket socket)
    {
        _socket = socket;
    }

    public long EventSequence => Interlocked.Read(ref _eventSequence);

    public static async Task<CdpConnection> ConnectAsync(Uri pageWebSocketUrl, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(pageWebSocketUrl, ct);
        var connection = new CdpConnection(socket);
        connection._readerTask = Task.Run(connection.ReadLoopAsync, CancellationToken.None);
        return connection;
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextMessageId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        await _sendGate.WaitAsync(ct);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            _sendGate.Release();
        }

        JsonElement response;
        try
        {
            response = await completion.Task.WaitAsync(CommandTimeout, ct);
        }
        catch (TimeoutException)
        {
            _pending.TryRemove(id, out _);
            throw;
        }

        if (response.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var text) ? text.GetString() : null;
            throw new InvalidOperationException($"CDP {method} failed: {message}");
        }

        return response.TryGetProperty("result", out var result) ? result : JsonDocument.Parse("null").RootElement;
    }

    public async Task<JsonElement> WaitForEventAsync(
        string method,
        long afterSequence,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (TryTakeEvent(method, afterSequence, out var payload))
            {
                return payload;
            }

            await Task.Delay(50, ct);
        }

        throw new TimeoutException($"CDP event {method} never arrived within {timeout.TotalSeconds}s");
    }

    public ValueTask DisposeAsync()
    {
        FailPendingMessages();
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            _socket.Abort();
        }

        _socket.Dispose();
        _sendGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private bool TryTakeEvent(string method, long afterSequence, out JsonElement payload)
    {
        while (_events.TryDequeue(out var candidate))
        {
            if (candidate.Method == method && candidate.Sequence > afterSequence)
            {
                payload = candidate.Payload;
                return true;
            }
        }

        payload = default;
        return false;
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[128 * 1024];
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        FailPendingMessages();
                        return;
                    }

                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                HandleMessage(JsonSerializer.Deserialize<JsonElement>(message.ToArray()));
            }
        }
        catch (Exception)
        {
            FailPendingMessages();
        }
    }

    private void HandleMessage(JsonElement message)
    {
        if (message.TryGetProperty("id", out var id))
        {
            if (_pending.TryRemove(id.GetInt64(), out var completion))
            {
                completion.TrySetResult(message.Clone());
            }

            return;
        }

        if (message.TryGetProperty("method", out var method))
        {
            var sequence = Interlocked.Increment(ref _eventSequence);
            _events.Enqueue(new CdpEvent(
                sequence,
                method.GetString()!,
                message.TryGetProperty("params", out var parameters) ? parameters.Clone() : JsonDocument.Parse("null").RootElement));
        }
    }

    private void FailPendingMessages()
    {
        foreach (var pair in _pending)
        {
            if (_pending.TryRemove(pair.Key, out var completion))
            {
                completion.TrySetException(new InvalidOperationException("devtools connection closed"));
            }
        }
    }
}
