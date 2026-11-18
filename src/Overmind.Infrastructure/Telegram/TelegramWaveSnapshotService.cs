using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Wave;
using Telegram.Bot.Types.ReplyMarkups;

namespace Overmind.Infrastructure.Telegram;

public sealed class TelegramWaveSnapshotService(
    TelegramReplySender sender,
    WaveSnapshotService snapshots,
    WaveChatService chat,
    OvermindDbContext db,
    ILogger<TelegramWaveSnapshotService> logger) : ITelegramWaveSnapshotService
{
    public const string PickerText = "Which project's wave do you want to snapshot?";
    public const string PickerTruncationFormat =
        "Which project's wave? (the first {0} by name, {1} more not shown)";
    public const string NoProjectsReply =
        "No projects are registered, so there is no wave to snapshot.";
    public const string SnapshotAnswer = "Snapshot sent.";
    public const string DigestFailedReply =
        "The wave snapshot could not be assembled; the failure was logged. Try again later.";
    public static readonly TimeSpan MentionWindow = TimeSpan.FromHours(24);

    public async Task HandleCommandAsync(long chatId, CancellationToken ct)
    {
        var (choices, hidden) = await ListProjectsAsync(ct);
        if (choices.Count == 0)
        {
            await SendQuietlyAsync(chatId, NoProjectsReply, ct);
            return;
        }

        if (choices.Count == 1)
        {
            await SendDigestQuietlyAsync(chatId, choices[0], ct);
            return;
        }

        await SendQuietlyAsync(
            chatId,
            hidden > 0 ? string.Format(PickerTruncationFormat, choices.Count, hidden) : PickerText,
            ct,
            TelegramWaveKeyboardMarkup.SnapshotProjects(choices));
    }

    public async Task<string> PickProjectAsync(long chatId, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            logger.LogWarning(
                "Telegram wave snapshot pick {ProjectId} in chat {ChatId} no longer resolves",
                projectId, chatId);
            return TelegramNotificationCallbackHandler.FailureAnswer;
        }

        var digest = TelegramWaveSnapshotFormatter.Format(
            await ComposeDigestAsync(new TelegramWaveProjectChoice(project.Id, project.Name), ct));
        await sender.SendAsync(chatId, TelegramReplySplitter.Split(digest), ct: ct);
        return SnapshotAnswer;
    }

    private async Task<TelegramWaveSnapshotFormatter.Digest> ComposeDigestAsync(
        TelegramWaveProjectChoice project, CancellationToken ct)
    {
        var snapshot = await snapshots.SnapshotAsync(project.Id, [], ct: ct);
        var recent = await chat.ListSinceAsync(project.Id, snapshot.GeneratedAt - MentionWindow, ct: ct);
        var rooms = await chat.ListRoomsAsync(project.Id, ct: ct);

        return new TelegramWaveSnapshotFormatter.Digest(
            project.Name,
            snapshot.GeneratedAt,
            RoomTraffic(rooms, recent),
            Holders(snapshot),
            [.. snapshot.QueueFeatures.Select(f => new TelegramWaveSnapshotFormatter.QueueRow(
                f.Slug, f.Priority, f.Status))],
            Blockers(snapshot),
            recent.Count);
    }

    private static List<TelegramWaveSnapshotFormatter.RoomTraffic> RoomTraffic(
        IReadOnlyList<WaveRoomSummary> rooms, IReadOnlyList<WaveMessage> recent)
    {
        var mentioned = recent
            .Where(message => message.MentionList.Count > 0)
            .ToLookup(message => message.Room, StringComparer.OrdinalIgnoreCase);

        return [.. rooms
            .Select(room => (room.Room, Hits: mentioned[room.Room]))
            .Where(entry => entry.Hits.Any())
            .Select(entry => new TelegramWaveSnapshotFormatter.RoomTraffic(
                entry.Room, entry.Hits.Count(), entry.Hits.Max(message => message.CreatedAt)))];
    }

    private static List<TelegramWaveSnapshotFormatter.Holder> Holders(WaveSnapshotData snapshot)
    {
        return [.. snapshot.QueueFeatures
            .Where(f => f.ClaimSessionId is not null)
            .Select(f => new TelegramWaveSnapshotFormatter.Holder(
                f.Slug,
                f.ClaimedByAlias ?? AgentSession.PlaceholderAlias,
                f.ClaimedByVendor,
                snapshot.ClaimLiveness.TryGetValue(f.Id, out var state)
                    ? state
                    : AgentSessionLiveness.Released,
                f.Lane,
                f.Lane is { } lane ? snapshot.LaneBoundAliases.GetValueOrDefault(lane) : null))];
    }

    private static List<TelegramWaveSnapshotFormatter.Blocker> Blockers(WaveSnapshotData snapshot)
    {
        var byId = snapshot.QueueFeatures.ToDictionary(f => f.Id);
        var open = new List<TelegramWaveSnapshotFormatter.Blocker>();
        foreach (var feature in snapshot.QueueFeatures)
        {
            foreach (var edge in feature.BlockedBy)
            {
                if (byId.TryGetValue(edge.BlockedByFeatureId, out var blocker)
                    && blocker.Status is not (PlanningFeatureStatus.Done or PlanningFeatureStatus.Review))
                {
                    open.Add(new TelegramWaveSnapshotFormatter.Blocker(feature.Slug, blocker.Slug, blocker.Status));
                }
            }
        }

        return open;
    }

    private async Task<(IReadOnlyList<TelegramWaveProjectChoice> Choices, int Hidden)> ListProjectsAsync(
        CancellationToken ct)
    {
        var rows = await db.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .Take(TelegramWaveCommandService.MaxProjectButtons + 1)
            .ToListAsync(ct);

        var hidden = 0;
        if (rows.Count > TelegramWaveCommandService.MaxProjectButtons)
        {
            hidden = await db.Projects.CountAsync(ct) - TelegramWaveCommandService.MaxProjectButtons;
            rows = [.. rows.Take(TelegramWaveCommandService.MaxProjectButtons)];
        }

        return ([.. rows.Select(p => new TelegramWaveProjectChoice(p.Id, p.Name))], hidden);
    }

    private async Task SendDigestQuietlyAsync(
        long chatId, TelegramWaveProjectChoice project, CancellationToken ct)
    {
        try
        {
            var digest = TelegramWaveSnapshotFormatter.Format(await ComposeDigestAsync(project, ct));
            await sender.SendAsync(chatId, TelegramReplySplitter.Split(digest), ct: ct);
            logger.LogInformation(
                "Telegram chat {ChatId} received the wave snapshot of project {ProjectId}",
                chatId, project.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Telegram wave snapshot of project {ProjectId} for chat {ChatId} could not be assembled",
                project.Id, chatId);
            await SendQuietlyAsync(chatId, DigestFailedReply, ct);
        }
    }

    private async Task SendQuietlyAsync(
        long chatId, string text, CancellationToken ct, ReplyMarkup? markup = null)
    {
        try
        {
            await sender.SendAsync(chatId, TelegramReplySplitter.Split(text), markup, ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Telegram wave snapshot notice to chat {ChatId} could not be delivered", chatId);
        }
    }
}
