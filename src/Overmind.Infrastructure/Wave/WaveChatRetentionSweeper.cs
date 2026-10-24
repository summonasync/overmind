using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Wave;

public sealed class WaveChatRetentionSweeper
{
    private readonly OvermindDbContext _db;

    public WaveChatRetentionSweeper(OvermindDbContext db)
    {
        _db = db;
    }

    public const int DefaultRetentionDays = 7;

    public async Task<int> SweepAsync(int retentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-NormalizeDays(retentionDays));

        return await _db.WaveMessages
            .Where(m => m.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    public static int NormalizeDays(int? configured) =>
        configured is { } days && days >= 1 ? days : DefaultRetentionDays;
}
