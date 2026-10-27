namespace Overmind.Mcp;

public static class AdapterSession
{
    // Reference holder: swaps of a boxed id are atomic for readers, so a handshake
    // re-registration never tears the 16-byte Guid while a request stamps its header.
    private sealed record SessionId(Guid Value);

    private static SessionId? _current;

    public static Guid? Id
    {
        get => _current?.Value;
        set => _current = value is { } id ? new SessionId(id) : null;
    }

    public const string HeaderName = "X-Overmind-Session";
}
