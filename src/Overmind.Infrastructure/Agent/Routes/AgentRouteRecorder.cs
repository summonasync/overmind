using System.Collections.Concurrent;

namespace Overmind.Infrastructure.Agent.Routes;

public sealed class AgentRouteRecorder(IAgentRouteStore store)
{
    public const string NotAnalyzedReason = "not analyzed";
    public const string UnknownDomain = "unknown";

    private readonly ConcurrentDictionary<Guid, bool> _analyzed = [];

    public void BeginTurn(Guid conversationId) => _analyzed[conversationId] = false;

    public bool TurnAnalyzed(Guid conversationId) =>
        _analyzed.TryGetValue(conversationId, out var analyzed) && analyzed;

    public async Task<AgentRouteRecord> RecordAnalysisAsync(
        Guid conversationId, string pattern, string domain, string missReason, CancellationToken ct = default)
    {
        var row = await store.RecordAsync(new AgentRouteRecording(pattern, domain, missReason), ct);
        _analyzed[conversationId] = true;
        return row;
    }

    public async Task RecordUnanalyzedAsync(Guid conversationId, string userText, CancellationToken ct = default)
    {
        if (TurnAnalyzed(conversationId))
        {
            return;
        }

        await store.RecordAsync(
            new AgentRouteRecording(AgentRouteText.Normalize(userText), UnknownDomain, NotAnalyzedReason), ct);
        _analyzed[conversationId] = true;
    }
}
