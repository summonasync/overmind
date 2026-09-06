using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.VectorSearch;

public sealed class EntryIndexingService
{
    private readonly QdrantSemanticSearch _semanticSearch;
    private readonly ILogger<EntryIndexingService> _logger;

    public EntryIndexingService(QdrantSemanticSearch semanticSearch, ILogger<EntryIndexingService> logger)
    {
        _semanticSearch = semanticSearch;
        _logger = logger;
    }

    public async Task IndexAsync(ContextEntry entry, CancellationToken ct)
    {
        try
        {
            await _semanticSearch.UpsertEntryAsync(entry, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to index entry {EntryId} for semantic search; full-text search remains authoritative",
                entry.Id);
        }
    }

    public async Task RemoveAsync(Guid entryId, CancellationToken ct)
    {
        try
        {
            await _semanticSearch.DeleteEntryAsync(entryId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove entry {EntryId} from the vector index", entryId);
        }
    }
}
