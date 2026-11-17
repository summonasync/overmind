using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

public sealed class SessionIdentityRefresher(
    AgentSessionLifecycle lifecycle,
    SessionIdentityFileWriter identityFile,
    WorkspaceIdentityFile workspaceIdentity)
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
            // The slot outranks env: a reconnect must not clobber the identity set at runtime.
            var slot = AdapterIdentityState.TryGet();
            var (alias, vendor) = slot is not null
                ? (slot.Alias, slot.Vendor)
                : (AgentIdentity.ResolveAlias(), AgentIdentity.ResolveVendor());
            var session = await lifecycle.RegisterAsync(alias, vendor, timeout.Token);
            Guid? superseded;
            lock (_swap)
            {
                superseded = AdapterSession.Id;
                AdapterSession.Id = session;
            }

            (alias, vendor) = await RestoreWorkspaceIdentityAsync(session, alias, vendor, ct);

            identityFile.Write(alias, vendor, session);

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

    public async Task<(string Alias, string Vendor)> RestoreWorkspaceIdentityAsync(
        Guid sessionId, string alias, string vendor, CancellationToken ct)
    {
        if (AdapterIdentityState.TryGet() is not null || !AgentIdentity.IsPlaceholderAlias(alias))
        {
            return (alias, vendor);
        }

        var saved = workspaceIdentity.TryRead();
        if (saved is null)
        {
            return (alias, vendor);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Budget);
        try
        {
            if (await lifecycle.TrySetIdentityAsync(sessionId, saved.Alias, saved.Vendor, timeout.Token))
            {
                Console.Error.WriteLine(
                    $"[overmind-mcp] workspace identity restored: {saved.Alias} by {saved.Vendor}");
                return (saved.Alias, saved.Vendor);
            }

            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: workspace identity '{saved.Alias}' by '{saved.Vendor}' is held by an " +
                $"alive session, registering as '{alias}' by '{vendor}' instead");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: workspace identity '{saved.Alias}' by '{saved.Vendor}' not restored, " +
                $"registering as '{alias}' by '{vendor}': {ex.Message}");
        }

        return (alias, vendor);
    }
}
