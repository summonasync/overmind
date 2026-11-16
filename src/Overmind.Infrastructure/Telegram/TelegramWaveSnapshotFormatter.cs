using System.Text;
using Overmind.Core;
using Overmind.Infrastructure.Wave;

namespace Overmind.Infrastructure.Telegram;

public static class TelegramWaveSnapshotFormatter
{
    public const int MaxRoomRows = 8;
    public const int MaxHolderRows = 8;
    public const int MaxQueueRows = 5;
    public const int MaxBlockerRows = 5;
    public const string IdleLine = "The wave is idle.";
    public const string MentionWindowLabel = "24h";

    public sealed record Digest(
        string ProjectName,
        DateTime GeneratedAt,
        IReadOnlyList<RoomTraffic> Rooms,
        IReadOnlyList<Holder> Holders,
        IReadOnlyList<QueueRow> QueueHead,
        IReadOnlyList<Blocker> Blockers,
        int WindowMessagesRead);

    public sealed record RoomTraffic(string Room, int MentionCount, DateTime? LastActivityAt);

    public sealed record Holder(
        string Slug,
        string Alias,
        string? Vendor,
        AgentSessionLiveness ClaimState,
        string? Lane,
        string? BoundAlias);

    public sealed record QueueRow(string Slug, FeaturePriority Priority, PlanningFeatureStatus Status);

    public sealed record Blocker(string Slug, string BlockerSlug, PlanningFeatureStatus BlockerStatus);

    public static string Format(Digest digest)
    {
        var rooms = RoomLines(digest.Rooms, digest.GeneratedAt);
        var holders = HolderLines(digest.Holders);
        var queue = QueueLines(digest.QueueHead);
        var blockers = BlockerLines(digest.Blockers);

        if (rooms.Count + holders.Count + queue.Count + blockers.Count == 0)
        {
            return $"Wave · {digest.ProjectName}\n{IdleLine}";
        }

        var body = new StringBuilder($"Wave · {digest.ProjectName}");
        AppendSection(
            body,
            $"Mentions ({MentionWindowLabel})",
            rooms,
            WindowFloorNote(digest.WindowMessagesRead));
        AppendSection(body, "Holders", holders);
        AppendSection(body, "Queue head", queue);
        AppendSection(body, "Blockers", blockers);
        return body.ToString();
    }

    private const string EmptyLine = "  none";

    private static void AppendSection(
        StringBuilder body, string header, IReadOnlyList<string> lines, string? footnote = null)
    {
        body.Append('\n').Append('\n').Append(header);
        foreach (var line in lines.Count == 0 ? [EmptyLine] : lines)
        {
            body.Append('\n').Append(line);
        }

        if (footnote is { Length: > 0 })
        {
            body.Append('\n').Append(footnote);
        }
    }

    private static string? WindowFloorNote(int windowMessagesRead) =>
        windowMessagesRead >= WaveChatService.MaxHistoryLimit
            ? $"  +mention counts cover the first {WaveChatService.MaxHistoryLimit} messages"
              + $" of the {MentionWindowLabel} window"
            : null;

    private static List<string> RoomLines(IReadOnlyList<RoomTraffic> rooms, DateTime generatedAt)
    {
        var shown = rooms.Take(MaxRoomRows)
            .Select(room => $"  {room.Room} · {room.MentionCount} mentions · last " +
                            RelativeAge(room.LastActivityAt, generatedAt))
            .ToList();
        AddHiddenTail(shown, rooms.Count, "rooms still have mention traffic");
        return shown;
    }

    private static List<string> HolderLines(IReadOnlyList<Holder> holders)
    {
        var shown = holders.Take(MaxHolderRows)
            .Select(holder =>
            {
                var line = new StringBuilder($"  {holder.Slug} · {holder.Alias}");
                if (holder.Vendor is { Length: > 0 } vendor)
                {
                    line.Append('/').Append(vendor);
                }

                line.Append(" [").Append(ClaimStateWord(holder.ClaimState)).Append(']');
                if (holder.Lane is { Length: > 0 } lane)
                {
                    line.Append(" lane ").Append(lane);
                    if (holder.BoundAlias is { Length: > 0 } bound)
                    {
                        line.Append(" (bound ").Append(bound).Append(')');
                    }
                }

                return line.ToString();
            })
            .ToList();
        AddHiddenTail(shown, holders.Count, "holders not shown");
        return shown;
    }

    private static List<string> QueueLines(IReadOnlyList<QueueRow> queue)
    {
        var shown = queue.Take(MaxQueueRows)
            .Select((row, index) =>
                $"  {index + 1}. p{(int)row.Priority} {row.Slug} [{StatusWord(row.Status)}]")
            .ToList();
        AddHiddenTail(shown, queue.Count, "more in the queue");
        return shown;
    }

    private static List<string> BlockerLines(IReadOnlyList<Blocker> blockers)
    {
        var shown = blockers.Take(MaxBlockerRows)
            .Select(blocker =>
                $"  {blocker.Slug} waits on {blocker.BlockerSlug} [{StatusWord(blocker.BlockerStatus)}]")
            .ToList();
        AddHiddenTail(shown, blockers.Count, "more blocked edges");
        return shown;
    }

    private static void AddHiddenTail(List<string> shown, int total, string hiddenTail)
    {
        if (total > shown.Count)
        {
            shown.Add($"  +{total - shown.Count} {hiddenTail}");
        }
    }

    private static string ClaimStateWord(AgentSessionLiveness state) => state switch
    {
        AgentSessionLiveness.Alive => "alive",
        AgentSessionLiveness.Stale => "stale",
        AgentSessionLiveness.Ended => "ended",
        _ => "released",
    };

    private static string StatusWord(PlanningFeatureStatus status) => status switch
    {
        PlanningFeatureStatus.Draft => "draft",
        PlanningFeatureStatus.Ready => "ready",
        PlanningFeatureStatus.InProgress => "in progress",
        PlanningFeatureStatus.Review => "review",
        _ => "done",
    };

    private static string RelativeAge(DateTime? at, DateTime now)
    {
        if (at is null)
        {
            return "never";
        }

        var age = now - at.Value;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}m";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours}h";
        }

        return $"{(int)age.TotalDays}d";
    }
}
