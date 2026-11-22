using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

public sealed class SessionIdentityRefresher(
    AgentSessionLifecycle lifecycle,
    SessionIdentityFileWriter identityFile,
    WorkspaceIdentityFile workspaceIdentity)
{
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    // The transient-failure bound: a second try inside the same Budget window, so one blip does not
    // cost the session its identity until the next reconnect.
    public const int RestoreAttempts = 2;

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
            var saved = workspaceIdentity.TryRead();
            var session = await lifecycle.RegisterAsync(alias, vendor, saved?.SessionId, timeout.Token);
            Guid? superseded;
            lock (_swap)
            {
                superseded = AdapterSession.Id;
                AdapterSession.Id = session;
            }

            if (saved is { } stamp && session == stamp.SessionId)
            {
                // The host re-attached the stamped row: its alias+vendor came back with it, so the
                // restore leg has nothing to negotiate — refresh the stamp and move on. AdapterIdentityState
                // stays untouched on purpose: setting it would turn later reconnects into explicit-alias
                // registrations, and each of those mints a fresh row instead of resuming this one.
                try
                {
                    identityFile.Write(stamp.Alias, stamp.Vendor, session);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Console.Error.WriteLine(
                        $"[overmind-mcp] WARN: session {session} is resumed; its identity stamp failed: {ex.Message}");
                }

                Console.Error.WriteLine($"[overmind-mcp] session {session} resumed on handshake");
            }
            else
            {
                (alias, vendor) = await RestoreAndStampAsync(session, alias, vendor, ct);
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

    // Registration is settled before this runs, so nothing here may be blamed on it: an unexpected
    // throw in the restore or in the stamp is reported as identity bookkeeping, and a stamp failure
    // must not undo an identity the host already accepted.
    public async Task<(string Alias, string Vendor)> RestoreAndStampAsync(
        Guid sessionId, string alias, string vendor, CancellationToken ct)
    {
        (string Alias, string Vendor) restored;
        try
        {
            restored = await RestoreWorkspaceIdentityAsync(sessionId, alias, vendor, ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: session {sessionId} is registered; its workspace identity was not " +
                $"restored: {ex.Message}");
            restored = (alias, vendor);
        }

        try
        {
            identityFile.Write(restored.Alias, restored.Vendor, sessionId);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: session {sessionId} is registered; its identity stamp failed: " +
                $"{ex.Message}");
        }

        return restored;
    }

    private async Task<(string Alias, string Vendor)> RestoreWorkspaceIdentityAsync(
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

        AnnounceVendorDiscard(vendor, saved);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Budget);

        var attempts = 0;
        var refused = false;
        string? detail = null;
        while (attempts < RestoreAttempts)
        {
            attempts++;
            try
            {
                var (applied, hostDetail) = await lifecycle.TrySetIdentityAsync(
                    sessionId, saved.Alias, saved.Vendor, timeout.Token);
                if (applied)
                {
                    AdapterIdentityState.Set(saved.Alias, saved.Vendor);
                    Console.Error.WriteLine(
                        $"[overmind-mcp] workspace identity restored: {saved.Alias} by {saved.Vendor}");
                    return (saved.Alias, saved.Vendor);
                }

                refused = true;
                detail = hostDetail;
                break;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                detail = ex.Message;
                if (timeout.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        if (refused)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: workspace identity '{saved.Alias}' by '{saved.Vendor}' is held by an " +
                $"alive session, registering as '{alias}' by '{vendor}' instead" +
                (detail is null ? "." : $"; host: {detail}"));
            return (alias, vendor);
        }

        Console.Error.WriteLine(
            $"[overmind-mcp] WARN: workspace identity '{saved.Alias}' by '{saved.Vendor}' not restored after " +
            $"{attempts} attempt(s), registering as '{alias}' by '{vendor}': {detail ?? "no answer"}");
        return (alias, vendor);
    }

    // The saved pair is what held claims match on, alias AND vendor, so an operator's explicit
    // OVERMIND_VENDOR cannot replace the saved one without stranding those claims: the restore keeps
    // the saved vendor and says out loud that the explicit setting was dropped.
    private static void AnnounceVendorDiscard(string vendor, SessionIdentitySnapshot saved)
    {
        if (OperatorVendor() is not { } operatorVendor ||
            operatorVendor.Equals(saved.Vendor, StringComparison.Ordinal) ||
            !vendor.Equals(operatorVendor, StringComparison.Ordinal))
        {
            return;
        }

        Console.Error.WriteLine(
            $"[overmind-mcp] WARN: explicit OVERMIND_VENDOR '{operatorVendor}' discarded — the workspace " +
            $"identity stays '{saved.Alias}' by '{saved.Vendor}', the pair held claims match on");
    }

    private static string? OperatorVendor()
    {
        var value = Environment.GetEnvironmentVariable("OVERMIND_VENDOR");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
