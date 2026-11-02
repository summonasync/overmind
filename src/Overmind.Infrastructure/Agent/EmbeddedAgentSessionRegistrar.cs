namespace Overmind.Infrastructure.Agent;

public sealed class EmbeddedAgentSessionRegistrar(
    AgentLoopbackClient api,
    EmbeddedAgentSessionState state,
    IAgentRunSettingsProvider settingsProvider)
{
    public const string Vendor = "overmind-host";

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Guid> EnsureRegisteredAsync(CancellationToken ct)
    {
        if (state.IsRegistered)
        {
            return state.SessionId;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (state.IsRegistered)
            {
                return state.SessionId;
            }

            var settings = await settingsProvider.GetAsync(ct);
            var response = await api.PostAsync(
                "/api/agent/sessions",
                new { alias = settings.Alias, vendor = Vendor, role = Overmind.Core.AgentSession.EmbeddedRole },
                ct);

            var sessionId = response.GetProperty("id").GetGuid();
            state.Set(sessionId);
            return sessionId;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndAsync(CancellationToken ct)
    {
        if (!state.IsRegistered)
        {
            return;
        }

        try
        {
            await api.PostAsync($"/api/agent/sessions/{state.SessionId}/end", new { }, ct);
        }
        catch (LoopbackApiException)
        {
        }
    }
}
