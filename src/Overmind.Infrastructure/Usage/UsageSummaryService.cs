using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Usage;

public sealed class UsageSummaryService
{
    private const decimal PerMillion = 1_000_000m;

    private readonly OvermindDbContext _db;
    private readonly UsagePricingCatalog _pricing;

    public UsageSummaryService(OvermindDbContext db, UsagePricingCatalog pricing)
    {
        _db = db;
        _pricing = pricing;
    }

    public async Task<UsageSummary> SummarizeAsync(
        Guid projectId, DateTime? since, DateTime? until, CancellationToken ct = default)
    {
        var spans = await _db.FeatureClaimSpans.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync(ct);
        var records = await _db.UsageRecords.AsNoTracking()
            .Where(r => (since == null || r.Ts >= since) && (until == null || r.Ts <= until))
            .ToListAsync(ct);
        var slugs = await _db.PlanningFeatures.AsNoTracking()
            .Where(f => f.ProjectId == projectId)
            .ToDictionaryAsync(f => f.Id, f => f.Slug, ct);
        var prices = await _pricing.LoadAsync(ct);

        var windows = spans
            .GroupBy(s => (s.Alias, s.SessionId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var features = new Dictionary<Guid, UsageAccumulator>();
        var agents = new Dictionary<string, UsageAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var span = FindSpan(windows, record);
            if (span is null)
            {
                continue;
            }

            prices.TryGetValue(record.Model, out var price);
            var cost = price is null
                ? 0m
                : (record.InputTokens * price.Input
                   + record.OutputTokens * price.Output
                   + record.CacheReadTokens * price.CacheRead
                   + record.CacheWriteTokens * price.CacheWrite) / PerMillion;

            Accumulate(features, span.FeatureId).Add(record, cost);
            Accumulate(agents, span.Alias).Add(record, cost);
        }

        return new UsageSummary(
            projectId,
            since,
            until,
            [.. features
                .Select(pair => new UsageFeatureTotal(
                    pair.Key,
                    slugs.GetValueOrDefault(pair.Key, pair.Key.ToString()),
                    pair.Value.Records,
                    pair.Value.InputTokens,
                    pair.Value.OutputTokens,
                    pair.Value.CacheReadTokens,
                    pair.Value.CacheWriteTokens,
                    pair.Value.Cost))
                .OrderByDescending(t => t.Cost)
                .ThenBy(t => t.Slug)],
            [.. agents
                .Select(pair => new UsageAgentTotal(
                    pair.Key,
                    pair.Value.Records,
                    pair.Value.InputTokens,
                    pair.Value.OutputTokens,
                    pair.Value.CacheReadTokens,
                    pair.Value.CacheWriteTokens,
                    pair.Value.Cost))
                .OrderByDescending(t => t.Cost)
                .ThenBy(t => t.Alias)]);
    }

    private static FeatureClaimSpan? FindSpan(
        Dictionary<(string Alias, Guid SessionId), List<FeatureClaimSpan>> windows, UsageRecord record)
    {
        if (!windows.TryGetValue((record.Alias, record.SessionId), out var candidates))
        {
            return null;
        }

        FeatureClaimSpan? match = null;
        foreach (var span in candidates)
        {
            var inside = span.ClaimedAt <= record.Ts &&
                         (span.ReleasedAt is null || record.Ts < span.ReleasedAt);
            if (inside && (match is null || span.ClaimedAt > match.ClaimedAt))
            {
                match = span;
            }
        }

        return match;
    }

    private static UsageAccumulator Accumulate(Dictionary<Guid, UsageAccumulator> map, Guid key)
    {
        if (!map.TryGetValue(key, out var value))
        {
            value = new UsageAccumulator();
            map[key] = value;
        }

        return value;
    }

    private static UsageAccumulator Accumulate(
        Dictionary<string, UsageAccumulator> map, string key)
    {
        if (!map.TryGetValue(key, out var value))
        {
            value = new UsageAccumulator();
            map[key] = value;
        }

        return value;
    }

    private sealed class UsageAccumulator
    {
        public long Records;
        public long InputTokens;
        public long OutputTokens;
        public long CacheReadTokens;
        public long CacheWriteTokens;
        public decimal Cost;

        public void Add(UsageRecord record, decimal cost)
        {
            Records++;
            InputTokens += record.InputTokens;
            OutputTokens += record.OutputTokens;
            CacheReadTokens += record.CacheReadTokens;
            CacheWriteTokens += record.CacheWriteTokens;
            Cost += cost;
        }
    }
}
