using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.VectorSearch;

public sealed record ReindexReport(int Total, int Indexed, int Failed);

public sealed class ReindexService
{
    private readonly OvermindDbContext _dbContext;
    private readonly QdrantSemanticSearch _semanticSearch;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(
        OvermindDbContext dbContext,
        QdrantSemanticSearch semanticSearch,
        ILogger<ReindexService> logger)
    {
        _dbContext = dbContext;
        _semanticSearch = semanticSearch;
        _logger = logger;
    }

    public async Task<ReindexReport> ReindexAllAsync(CancellationToken ct)
    {
        var entries = await _dbContext.ContextEntries.AsNoTracking().ToListAsync(ct);

        var indexed = 0;
        var failed = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _semanticSearch.UpsertEntryAsync(entry, ct);
                indexed++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Reindex failed for entry {EntryId}", entry.Id);
            }
        }

        return new ReindexReport(entries.Count, indexed, failed);
    }
}
