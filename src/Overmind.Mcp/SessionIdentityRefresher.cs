using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

public sealed class SessionIdentityRefresher(AgentSessionLifecycle lifecycle)
{
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly object _swap = new();
    private int _handshakes;

    public McpMessageFilter CreateIncomingFilter() => next => async (context, cancellationToken) =>
    {
        if (context.JsonRpcMessage is JsonRpcRequest { Method: "initialize" })
        {
            await RefreshAsync(cancellationToken);
        }

        await next(context, cancellationToken);
    };

    public async Task RefreshAsync(CancellationToken ct)
    {
        var first = Interlocked.Increment(ref _handshakes) == 1;
        if (first && AdapterSession.Id is { } live && live != Guid.Empty)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Budget);
        try
        {
            var session = await lifecycle.RegisterAsync(
                AgentIdentity.ResolveAlias(), AgentIdentity.ResolveVendor(), timeout.Token);
            Guid? superseded;
            lock (_swap)
            {
                superseded = AdapterSession.Id;
                AdapterSession.Id = session;
            }

            if (superseded is { } old && old != Guid.Empty && old != session)
            {
                // Bounded on its own; a dead host must not hold the handshake open for it.
                _ = Task.Run(() => lifecycle.MarkEnded(old), CancellationToken.None);
            }

            Console.Error.WriteLine($"[overmind-mcp] session {session} registered on handshake");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: handshake registration timed out after {Budget.TotalSeconds:0}s, keeping session {AdapterSession.Id}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: handshake registration failed, keeping session {AdapterSession.Id}: {ex.Message}");
        }
    }
}
