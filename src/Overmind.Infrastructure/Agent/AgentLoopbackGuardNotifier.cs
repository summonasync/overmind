namespace Overmind.Infrastructure.Agent;

public sealed class AgentLoopbackGuardNotifier(
    AgentLoopbackClient api,
    IAgentRunSettingsProvider settingsProvider) : IAgentGuardNotifier
{
    public async Task NotifyGuardAsync(string title, string detail, Guid? projectId, CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        await api.PostAsync(
            "/api/agent/notifications",
            new
            {
                kind = "info",
                text = $"{title}. {detail}".Trim(),
                projectId,
                from = settings.Alias
            },
            ct);
    }
}
