using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

public sealed class GraphNodePositionWriter
{
    private readonly OvermindDbContext _db;

    public GraphNodePositionWriter(OvermindDbContext db)
    {
        _db = db;
    }

    // One act per request, so a mixed pin/unpin batch lands whole or not at all. Two writes naming
    // the same node in one batch collapse last-wins: the payload is a set of final wishes, not a
    // replay log, and refusing it would only make the canvas retry with the same tail.
    public async Task SaveAsync(IReadOnlyList<GraphNodePositionWrite> writes, CancellationToken ct = default)
    {
        if (writes.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collapsed = new Dictionary<(EntryLinkKind Kind, Guid Id), GraphNodePositionWrite>();
        foreach (var write in writes)
        {
            collapsed[(write.Kind, write.Id)] = write;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var ids = collapsed.Values.Select(write => write.Id).ToList();
        var existing = await _db.GraphNodePositions
            .Where(position => ids.Contains(position.NodeId))
            .ToListAsync(ct);

        foreach (var (key, write) in collapsed)
        {
            var row = existing.FirstOrDefault(position => position.NodeKind == key.Kind && position.NodeId == key.Id);
            if (row is null)
            {
                _db.GraphNodePositions.Add(
                    GraphNodePosition.Write(key.Kind, key.Id, write.X, write.Y, write.Pinned, now));
            }
            else if (write.Pinned)
            {
                row.PinAt(write.X, write.Y, now);
            }
            else
            {
                row.Release(write.X, write.Y, now);
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
