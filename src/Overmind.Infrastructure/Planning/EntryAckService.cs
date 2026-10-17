using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class EntryAckService
{
    private readonly OvermindDbContext _db;

    public EntryAckService(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<EntryAckOutcome> AckAsync(
        Guid projectId, Guid entryId, DirectiveAuthor author, CancellationToken ct = default)
    {
        var entry = await _db.ContextEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.ProjectId == projectId, ct)
            ?? throw PlanningException.MissingEntry(entryId);

        var alias = EntryAck.NormalizeIdentity(author.Alias);
        var vendor = EntryAck.NormalizeIdentity(author.Vendor);
        var existing = await _db.EntryAcks
            .FirstOrDefaultAsync(a =>
                a.ContextEntryId == entry.Id &&
                a.Alias == alias &&
                a.Vendor == vendor, ct);

        if (existing is not null)
        {
            return new EntryAckOutcome(existing, AckedNow: false);
        }

        var ack = EntryAck.Create(entry.Id, alias, vendor, DateTime.UtcNow);
        _db.EntryAcks.Add(ack);
        await _db.SaveChangesAsync(ct);
        return new EntryAckOutcome(ack, AckedNow: true);
    }

    public async Task<IReadOnlyList<EntryAck>> ListForEntryAsync(Guid entryId, CancellationToken ct = default) =>
        [.. await _db.EntryAcks
            .AsNoTracking()
            .Where(a => a.ContextEntryId == entryId)
            .OrderBy(a => a.At)
            .ThenBy(a => a.Id)
            .ToListAsync(ct)];
}
