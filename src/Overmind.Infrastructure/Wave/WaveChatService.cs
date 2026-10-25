using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Wave;

public sealed record WaveRoomSummary(string Room, DateTime? LastActivityAt);

public sealed record WaveUnreadPage(IReadOnlyList<WaveMessage> Messages, DateTime WatermarkAt);

public sealed class WaveChatService
{
    private readonly OvermindDbContext _db;

    public WaveChatService(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<WaveMessage> PostAsync(
        Guid projectId, string room, string authorAlias, string? text, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);

        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var trimmedText = text.Trim();
        if (trimmedText.Length > WaveMessage.MaxTextLength)
        {
            throw PlanningException.WaveTextTooLong(trimmedText.Length, WaveMessage.MaxTextLength);
        }

        var message = WaveMessage.Create(projectId, normalizedRoom, authorAlias, AgentSession.DefaultRole, text);
        _db.WaveMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<WaveUnreadPage> ListUnreadAsync(
        Guid projectId, string alias, string room, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var normalizedRoom = await RequireRoomAsync(projectId, room, ct);
        var normalizedAlias = WaveWatermark.NormalizeAlias(alias);

        var watermark = await _db.WaveWatermarks
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Alias == normalizedAlias, ct);
        var since = watermark?.LastReadAt ?? DateTime.MinValue;

        var mentionPattern = $"% {normalizedAlias.Replace("_", "\\_")} %";
        var messages = await _db.WaveMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Where(m => (m.Room == normalizedRoom && m.CreatedAt > since)
                        || EF.Functions.Like(m.Mentions, mentionPattern, "\\"))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return new WaveUnreadPage(messages, watermark?.LastReadAt ?? since);
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
