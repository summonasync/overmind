using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wake;

namespace Overmind.Infrastructure.Wave;

public sealed record WaveRoomSummary(string Room, DateTime? LastActivityAt);

public sealed record WaveUnreadPage(IReadOnlyList<WaveMessage> Messages, DateTime WatermarkAt);

public sealed record WaveUnreadPeek(string Room, int Count, IReadOnlyList<Guid> Ids);

public sealed class WaveChatService
{
    public const int DefaultHistoryLimit = 20;
    public const int MaxHistoryLimit = 500;

    private readonly OvermindDbContext _db;
    private readonly WakeDropFanout _wakeDrop;
    private readonly IWaveChatBroadcaster _broadcaster;

    public WaveChatService(OvermindDbContext db, WakeDropFanout wakeDrop, IWaveChatBroadcaster broadcaster)
    {
        _db = db;
        _wakeDrop = wakeDrop;
        _broadcaster = broadcaster;
    }

    public async Task<WaveMessage> PostAsync(
        Guid projectId, string room, string authorAlias, string authorRole, string? text, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);

        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var trimmedText = text.Trim();
        var hardCeiling = SoftCap.Ceiling(WaveMessage.MaxTextLength);
        if (trimmedText.Length > hardCeiling)
        {
            throw PlanningException.WaveTextTooLong(trimmedText.Length, hardCeiling);
        }

        var message = WaveMessage.Create(projectId, normalizedRoom, authorAlias, authorRole, text);
        _db.WaveMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        _broadcaster.Publish(projectId, normalizedRoom, message);
        foreach (var alias in message.MentionList)
        {
            _ = _wakeDrop.WakeAsync(alias);
        }

        return message;
    }

    public async Task<WaveUnreadPage> ListUnreadAsync(
        Guid projectId,
        string alias,
        string room,
        DateTime? since = null,
        int? limit = null,
        bool replayMentions = false,
        CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);
        var normalizedAlias = WaveWatermark.NormalizeAlias(alias);

        var watermark = await _db.WaveWatermarks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Alias == normalizedAlias, ct);
        var watermarkAt = watermark?.LastReadAt ?? DateTime.MinValue;
        var effectiveSince = since is { } requested && requested > watermarkAt ? requested : watermarkAt;

        var feed = UnreadFeed(projectId, normalizedRoom, normalizedAlias, effectiveSince, replayMentions);
        IReadOnlyList<WaveMessage> messages;
        if (limit is { } pageLimit)
        {
            var page = await feed
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(Math.Clamp(pageLimit, 1, MaxHistoryLimit))
                .ToListAsync(ct);
            page.Reverse();
            messages = page;
        }
        else
        {
            messages = await feed
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToListAsync(ct);
        }

        if (messages.Count == 0)
        {
            return new WaveUnreadPage(messages, watermarkAt);
        }

        var readAt = messages[^1].CreatedAt;
        if (watermark is null)
        {
            watermark = WaveWatermark.Create(projectId, normalizedAlias, readAt);
            _db.WaveWatermarks.Add(watermark);
        }
        else
        {
            watermark.Advance(readAt);
        }

        await _db.SaveChangesAsync(ct);
        return new WaveUnreadPage(messages, readAt);
    }

    public async Task<WaveUnreadPeek> PeekAsync(
        Guid projectId, string alias, string room, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);
        var normalizedAlias = WaveWatermark.NormalizeAlias(alias);

        var watermarkAt = await _db.WaveWatermarks
            .Where(w => w.ProjectId == projectId && w.Alias == normalizedAlias)
            .Select(w => (DateTime?)w.LastReadAt)
            .FirstOrDefaultAsync(ct) ?? DateTime.MinValue;

        var ids = await UnreadFeed(projectId, normalizedRoom, normalizedAlias, watermarkAt, replayMentions: false)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .ToListAsync(ct);

        return new WaveUnreadPeek(normalizedRoom, ids.Count, ids);
    }

    private IQueryable<WaveMessage> UnreadFeed(
        Guid projectId, string room, string alias, DateTime since, bool replayMentions)
    {
        var mentionPattern = WaveMentions.LikePattern(alias);
        var projectFeed = _db.WaveMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId);

        return replayMentions
            ? projectFeed.Where(m => (m.Room == room && m.CreatedAt > since)
                        || EF.Functions.Like(m.Mentions, mentionPattern, WaveMentions.Escape))
            : projectFeed.Where(m => m.CreatedAt > since
                        && (m.Room == room || EF.Functions.Like(m.Mentions, mentionPattern, WaveMentions.Escape)));
    }

    public async Task<IReadOnlyList<WaveMessage>> ListHistoryAsync(
        Guid projectId,
        string room,
        int? limit = null,
        DateTime? before = null,
        CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);
        var capped = Math.Clamp(limit ?? DefaultHistoryLimit, 1, MaxHistoryLimit);

        var page = await _db.WaveMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.Room == normalizedRoom)
            .Where(m => before == null || m.CreatedAt < before)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(capped)
            .ToListAsync(ct);

        page.Reverse();
        return page;
    }

    public async Task<IReadOnlyList<WaveMessage>> ListSinceAsync(
        Guid projectId,
        DateTime since,
        Guid? sinceId = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var capped = Math.Clamp(limit ?? MaxHistoryLimit, 1, MaxHistoryLimit);

        return await _db.WaveMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Where(m => m.CreatedAt > since
                || (m.CreatedAt == since && sinceId != null && m.Id.CompareTo(sinceId.Value) > 0))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(capped)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WaveRoomSummary>> ListRoomsAsync(Guid projectId, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);

        var lanes = await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Select(l => l.Name)
            .ToListAsync(ct);

        var activity = await _db.WaveMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .GroupBy(m => m.Room)
            .Select(g => new { Room = g.Key, LastActivityAt = g.Max(m => m.CreatedAt) })
            .ToListAsync(ct);

        var byRoom = activity.ToDictionary(
            a => a.Room, a => (DateTime?)a.LastActivityAt, StringComparer.OrdinalIgnoreCase);

        return
        [
            new WaveRoomSummary(WaveMessage.AllRooms, byRoom.GetValueOrDefault(WaveMessage.AllRooms)),
            .. lanes
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new WaveRoomSummary(n, byRoom.GetValueOrDefault(n)))
        ];
    }

    private async Task<string> RequireRoomAsync(Guid projectId, string room, CancellationToken ct)
    {
        var normalized = WaveMessage.NormalizeRoom(room);
        if (normalized == WaveMessage.AllRooms)
        {
            return normalized;
        }

        var roster = await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Select(l => l.Name)
            .ToListAsync(ct);

        return roster.FirstOrDefault(n => n.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw PlanningException.MissingWaveRoom(normalized, roster);
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);
}
