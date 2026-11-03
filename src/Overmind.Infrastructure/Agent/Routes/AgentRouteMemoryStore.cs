namespace Overmind.Infrastructure.Agent.Routes;

public sealed class AgentRouteMemoryStore : IAgentRouteStore
{
    public const int PromotionHits = 3;

    private readonly object _lock = new();
    private readonly Dictionary<Guid, AgentRouteRecord> _rows = [];

    public Task<AgentRouteRecord> RecordAsync(AgentRouteRecording recording, CancellationToken ct = default)
    {
        var pattern = AgentRouteText.Normalize(recording.Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var domain = AgentRouteText.Clamp(recording.Domain, AgentRouteText.MaxDomainLength);
        var missReason = AgentRouteText.Clamp(recording.MissReason, AgentRouteText.MaxMissReasonLength);

        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var existing = _rows.Values.FirstOrDefault(row => row.Pattern == pattern);
            if (existing is { } row)
            {
                var hitCount = row.HitCount + 1;
                var updated = row with
                {
                    Domain = domain.Length > 0 ? domain : row.Domain,
                    MissReason = missReason.Length > 0 ? missReason : row.MissReason,
                    HitCount = hitCount,
                    LastHitAt = now,
                    Status = hitCount >= PromotionHits ? AgentRouteStatus.Confirmed : row.Status
                };
                _rows[updated.Id] = updated;
                return Task.FromResult(updated);
            }

            var created = new AgentRouteRecord(
                Guid.NewGuid(), pattern, domain, null, missReason,
                AgentRouteStatus.Provisional, 1, AgentRouteSource.Learned, now, now);
            _rows[created.Id] = created;
            return Task.FromResult(created);
        }
    }

    public Task<IReadOnlyList<AgentRouteRecord>> HotHeadAsync(int maxResults, CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<AgentRouteRecord> head = _rows.Values
                .OrderByDescending(row => row.HitCount)
                .ThenByDescending(row => row.LastHitAt)
                .Take(Math.Max(1, maxResults))
                .ToList();
            return Task.FromResult(head);
        }
    }

    public Task<bool> PromoteAsync(Guid id, CancellationToken ct = default) => SetStatus(id, AgentRouteStatus.Confirmed);

    public Task<bool> DemoteAsync(Guid id, CancellationToken ct = default) => SetStatus(id, AgentRouteStatus.Provisional);

    public Task<int> ClearAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var removed = _rows.Count;
            _rows.Clear();
            return Task.FromResult(removed);
        }
    }

    private Task<bool> SetStatus(Guid id, AgentRouteStatus status)
    {
        lock (_lock)
        {
            if (!_rows.TryGetValue(id, out var row))
            {
                return Task.FromResult(false);
            }

            _rows[id] = row with { Status = status };
            return Task.FromResult(true);
        }
    }
}
