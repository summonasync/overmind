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

        var resolved = await _resolver.ResolveAsync(entryId, parsedLinks, ct);
        if (resolved.Count > 0)
        {
            _db.EntryLinks.AddRange(resolved.Select(link =>
                EntryLink.CreateDerived(entryId, link.Kind, link.ToId)));
        }

        await _db.SaveChangesAsync(ct);
        return resolved.Count;
    }
}
