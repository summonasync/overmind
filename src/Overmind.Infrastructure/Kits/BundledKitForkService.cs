using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

public sealed class BundledKitForkService
{
    private readonly OvermindDbContext _db;
    private readonly ILogger<BundledKitForkService> _logger;

    public BundledKitForkService(OvermindDbContext db, ILogger<BundledKitForkService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ForkExclusiveBundledOwnerAsync(Guid entryId, CancellationToken ct)
    {
        var owners = await _db.SkillKits
            .Where(k => k.SkillKitEntries.Any(link => link.EntryId == entryId))
            .ToListAsync(ct);

        if (owners.Count != 1 || owners[0].SourceKind != KitSourceKind.Bundled)
        {
            return;
        }

        var kit = owners[0];
        kit.ForkToManual();
        _logger.LogInformation(
            "Bundled kit {KitId} '{KitName}' forked to manual by content edit of entry {EntryId}",
            kit.Id, kit.Name, entryId);
    }
}
