namespace Overmind.Infrastructure.Agent;

public sealed class EmbeddedAgentSessionState
{
    private volatile object? _sessionId;

    public Guid SessionId => _sessionId is Guid id ? id : Guid.Empty;

    public bool IsRegistered => _sessionId is not null;

    public void Set(Guid sessionId) => _sessionId = sessionId;
}
