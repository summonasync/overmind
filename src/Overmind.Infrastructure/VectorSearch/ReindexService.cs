using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.VectorSearch;

public sealed record ReindexReport(
    int Total,
    int Indexed,
    int Failed,
    CollectionRebuildReport? Collection = null);

public sealed class ReindexService
{
    private readonly OvermindDbContext _dbContext;
    private readonly QdrantSemanticSearch _semanticSearch;
    private readonly QdrantCollectionSync _collectionSync;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(
        OvermindDbContext dbContext,
        QdrantSemanticSearch semanticSearch,
        ILogger<ReindexService> logger)
    {
        _dbContext = dbContext;
        _semanticSearch = semanticSearch;
        _collectionSync = new QdrantCollectionSync(QdrantCollection.Name, logger);
        _logger = logger;
    }

    public async Task<ReindexReport> RebuildAllAsync(CancellationToken ct)
    {
        var rebuild = await RebuildCollectionAsync(ct);
        var report = await ReindexAllAsync(ct);
        return report with { Collection = rebuild };
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

    private async Task<CollectionRebuildReport?> RebuildCollectionAsync(CancellationToken ct)
    {
        try
        {
            var fingerprint = await _semanticSearch.CurrentFingerprintAsync(ct);
            var client = await _semanticSearch.ClientAsync(ct);
            return await _collectionSync.RebuildAsync(client, fingerprint, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Vector collection rebuild skipped, reindexing the existing collection in place: {Reason}",
                ex.Message);
            return null;
        }
    }
}
