using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;

namespace Overmind.Infrastructure.Wake;

public sealed record AgentWakeSignal(
    string Kind,
    Guid RefId,
    string? RoomId,
    string From,
    DateTime CreatedAt,
    string Excerpt);

public sealed record AgentWakePage(
    IReadOnlyList<AgentWakeSignal> Signals, DateTime Cursor, Guid? CursorRefId);

public sealed class AgentWakeService(OvermindDbContext db)
{
    public const string ReplyKind = "notification-reply";
    public const string MentionKind = "wave-mention";
    public const string ReplyFrom = "human";
    public const int PageSize = 100;
    public const int ExcerptLength = 200;

    private static readonly TimeSpan ReplyLookback = TimeSpan.FromDays(7);

    public async Task<AgentWakePage?> SnapshotAsync(string alias, CancellationToken ct = default)
    {
        var lastSeenAt = await db.AgentSessions
            .AsNoTracking()
            .Where(s => s.Alias == alias)
            .MaxAsync(s => (DateTime?)s.LastSeenAt, ct);

        return lastSeenAt is null ? null : await PendingAsync(alias, lastSeenAt.Value, null, null, ct);
    }

    public async Task<AgentWakePage> PendingAsync(
        string alias,
        DateTime lastSeenAt,
        DateTime? since,
        Guid? sinceRefId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var replySince = since ?? now - ReplyLookback;
        var mentionSince = since ?? lastSeenAt;

        var replies = await db.AgentNotifications
            .AsNoTracking()
            .Where(n => n.Agent == alias && n.ReplyText != null
                && (n.RepliedAt > replySince || sinceRefId != null && n.RepliedAt == replySince))
            .OrderBy(n => n.RepliedAt)
            .Select(n => new { n.Id, n.ReplyText, n.RepliedAt })
            .ToListAsync(ct);

        var mentionPattern = WaveMentions.LikePattern(WaveWatermark.NormalizeAlias(alias));
        var mentions = await db.WaveMessages
            .AsNoTracking()
            .Where(m => EF.Functions.Like(m.Mentions, mentionPattern, WaveMentions.Escape)
                && (m.CreatedAt > mentionSince
                    || sinceRefId != null && m.CreatedAt == mentionSince))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Room, m.AuthorAlias, m.Text, m.CreatedAt })
            .ToListAsync(ct);

        var signals = replies
            .Select(r => new AgentWakeSignal(
                ReplyKind, r.Id, null, ReplyFrom, r.RepliedAt!.Value, Excerpt(r.ReplyText!)))
            .Concat(mentions.Select(m => new AgentWakeSignal(
                MentionKind, m.Id, m.Room, m.AuthorAlias, m.CreatedAt, Excerpt(m.Text))))
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.RefId)
            .ToList();

        if (since is { } cursorAt && sinceRefId is { } cursorRefId)
        {
            signals.RemoveAll(s => s.CreatedAt == cursorAt && s.RefId.CompareTo(cursorRefId) <= 0);
        }

        var page = signals.Take(PageSize).ToList();
        var cursor = page.Count > 0 ? page[^1].CreatedAt : since ?? now;
        return new AgentWakePage(page, cursor, page.Count > 0 ? page[^1].RefId : null);
    }

    private static string Excerpt(string text)
    {
        var info = new StringInfo(text);
        return info.LengthInTextElements <= ExcerptLength
            ? text
            : info.SubstringByTextElements(0, ExcerptLength);
    }
}
