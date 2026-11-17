using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Entries;

public sealed class EntryLinkWriter
{
    private readonly OvermindDbContext _db;
    private readonly EntryLinkResolver _resolver;

    public EntryLinkWriter(OvermindDbContext db, EntryLinkResolver resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    public async Task<int> ReplaceDerivedAsync(
        Guid entryId,
        IReadOnlyList<WikiLinkTarget> parsedLinks,
        IDbContextTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(_db.Database.CurrentTransaction, transaction))
        {
            throw new InvalidOperationException(
                $"EntryLinkWriter must run inside the caller's transaction (entry {entryId}).");
        }

        await _db.EntryLinks
            .Where(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Derived)
            .ExecuteDeleteAsync(ct);

        var manualKeys = await _db.EntryLinks
            .AsNoTracking()
            .Where(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Manual)
            .Select(link => new { link.ToKind, link.ToId })
            .ToListAsync(ct);

        var insertable = (await _resolver.ResolveForWriteAsync(entryId, parsedLinks, ct))
            .Where(link => !manualKeys.Any(manual =>
                manual.ToKind == link.Kind && manual.ToId == link.ToId))
            .ToList();

        if (insertable.Count > 0)
        {
            _db.EntryLinks.AddRange(insertable.Select(link =>
                EntryLink.CreateDerived(entryId, link.Kind, link.ToId)));
        }

        await _db.SaveChangesAsync(ct);
        return insertable.Count;
    }
}
