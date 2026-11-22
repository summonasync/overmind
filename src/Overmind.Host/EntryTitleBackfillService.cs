using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host;

// One-shot startup pass over the search-normalized titles. The migration adds the columns with an
// empty default because SQLite's lower() cannot fold Cyrillic inside SQL — the C# invariant lower
// is the only fold that works, so existing rows get re-folded here and every later write keeps the
// value correct at the entity level. Cheap after the first boot: the scan reads three light columns
// per row and the update pass shrinks to nothing. StartAsync runs the pass to completion on purpose —
// the API must not serve searches until the stored fold is consistent (this is not a BackgroundService).
public sealed class EntryTitleBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<EntryTitleBackfillService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

            var entries = await db.ContextEntries
                .Select(entry => new { entry.Id, entry.Title, entry.TitleNormalized })
                .ToListAsync(cancellationToken);
            var fixedEntries = 0;
            foreach (var row in entries.Where(row =>
                         !string.Equals(row.TitleNormalized, ContextEntry.NormalizeTitle(row.Title), StringComparison.Ordinal)))
            {
                await db.ContextEntries
                    .Where(entry => entry.Id == row.Id)
                    .ExecuteUpdateAsync(
                        set => set.SetProperty(entry => entry.TitleNormalized, ContextEntry.NormalizeTitle(row.Title)),
                        cancellationToken);
                fixedEntries++;
            }

            if (fixedEntries > 0)
            {
                logger.LogInformation("Title backfill re-folded {Entries} entries", fixedEntries);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Title backfill cancelled mid-pass; the stored fold is partially repaired");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Entry title backfill failed; refusing to serve searches over an inconsistent fold");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
