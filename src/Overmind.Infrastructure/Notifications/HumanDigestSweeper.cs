using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Notifications;

public sealed record HumanDigestResult(int ProjectsScanned, int DigestsPosted);

public sealed class HumanDigestSweeper(
    OvermindDbContext db,
    HumanInboxService inbox,
    NotificationsService notifications)
{
    public const string Agent = "human-inbox";
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public async Task<HumanDigestResult> SweepAsync(
        Func<Guid, string, string> featureLink, DateTime now, CancellationToken ct = default)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Where(p => p.HumanDigestEnabled)
            .ToListAsync(ct);

        var posted = 0;
        foreach (var project in projects)
        {
            var entries = await inbox.ListAsync(project.Id, ct);
            if (entries.Count == 0 || !IsDue(await LastDigestAtAsync(project.Id, ct), now))
            {
                continue;
            }

            await notifications.CreateAsync(
                NotificationKind.Info,
                DigestText(project.Id, entries, featureLink),
                project.Id,
                null,
                null,
                Agent,
                ct);
            posted++;
        }

        return new HumanDigestResult(projects.Count, posted);
    }

    internal static bool IsDue(DateTime? lastDigestAt, DateTime now) =>
        lastDigestAt is null || now - lastDigestAt.Value >= Interval;

    private async Task<DateTime?> LastDigestAtAsync(Guid projectId, CancellationToken ct) =>
        await db.AgentNotifications
            .AsNoTracking()
            .Where(n => n.ProjectId == projectId && n.Kind == NotificationKind.Info && n.Agent == Agent)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => (DateTime?)n.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static string DigestText(
        Guid projectId, IReadOnlyList<HumanInboxEntry> entries, Func<Guid, string, string> featureLink)
    {
        var header = entries.Count == 1
            ? "1 feature is waiting for your tap:"
            : $"{entries.Count.ToString(CultureInfo.InvariantCulture)} features are waiting for your tap:";

        var bullets = entries.Select(e =>
            $"• {e.Slug} — pending since {Stamp(e.PendingSince)} — {featureLink(projectId, e.Slug)}");

        return string.Join('\n', bullets.Prepend(header));
    }

    private static string Stamp(DateTime pendingSince) =>
        DateTime.SpecifyKind(pendingSince, DateTimeKind.Utc)
            .ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
