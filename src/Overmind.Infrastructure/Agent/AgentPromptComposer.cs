using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentPromptComposer(
    AgentLoopbackClient api,
    IAgentRunSettingsProvider settingsProvider) : IAgentPromptComposer
{
    public const int MaxHistoryMessages = 24;
    public const int MaxSnapshotProjects = 8;
    public const int MaxQueueRowsPerProject = 10;
    public const int MaxRecentReplies = 5;

    public async Task<IReadOnlyList<ChatMessage>> ComposeAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        CancellationToken ct)
    {
        var settings = await settingsProvider.GetAsync(ct);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, await ComposeSystemPromptAsync(settings, conversation.ProjectId, ct))
        };

        foreach (var row in history.Where(m => m.Role is ConversationMessage.UserRole or ConversationMessage.AssistantRole)
                     .TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatMessage(
                row.Role == ConversationMessage.UserRole ? ChatRole.User : ChatRole.Assistant,
                row.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, userText));
        return messages;
    }

    private async Task<string> ComposeSystemPromptAsync(AgentRunSettings settings, Guid? projectId, CancellationToken ct)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"You are {settings.Alias}, the Overmind embedded management agent running inside the Overmind Host. Today is {DateTime.UtcNow:yyyy-MM-dd}.");
        if (projectId is { } scopedProject)
        {
            builder.AppendLine($"The conversation is scoped to project {scopedProject}.");
        }

        builder.AppendLine();
        builder.AppendLine(
            """
            You drive the Overmind board - planning, entries, issues, lanes, wave chat, kits - through the same agent door external coding agents use. Claim locks, human doors, validation gates and materialization to BACKLOG/ are enforced server-side on every call; there is no bypass.

            Rules:
            - The board (database) is the source of truth; BACKLOG/ on disk is a read-only mirror. Never trust a status read from a file.
            - Always pass projectId explicitly; resolve it from the snapshot below or list_projects.
            - planning_validate and done flips are human-door actions: never call planning_validate, and never expect done to succeed. A draft spec or plan of yours waits for the human; delegated approvals are a separate capability.
            - Every planning mutation needs a logNote: say why.
            - Feature priority changes only on the user's explicit approval in this conversation.
            - If something surprises you, tell the human with notify_human instead of guessing.
            """);
        builder.AppendLine();
        builder.AppendLine("Bootstrap snapshot (may be partial):");
        builder.AppendLine(await ComposeSnapshotAsync(projectId, ct));
        return builder.ToString();
    }

    private async Task<string> ComposeSnapshotAsync(Guid? projectId, CancellationToken ct)
    {
        var snapshot = new Dictionary<string, object?>();

        try
        {
            var projects = await api.GetAsync("/api/projects", ct);
            List<Dictionary<string, object?>> projectRows = [.. EnumerateJson(projects)
                .Take(MaxSnapshotProjects)
                .Select(project => new Dictionary<string, object?>
                {
                    ["id"] = TextOf(project, "id"),
                    ["name"] = TextOf(project, "name"),
                    ["openFeatures"] = TextOf(project, "openFeatureCount")
                })];
            snapshot["projects"] = projectRows;

            var queue = new List<object>();
            foreach (var project in EnumerateJson(projects).Take(MaxSnapshotProjects))
            {
                if (!Guid.TryParse(TextOf(project, "id"), out var id) || (projectId is { } scoped && id != scoped))
                {
                    continue;
                }

                try
                {
                    var rows = await api.GetAsync($"/api/agent/projects/{id}/planning/task-queue", ct);
                    if (rows.TryGetProperty("items", out var items))
                    {
                        queue.AddRange(items.EnumerateArray().Take(MaxQueueRowsPerProject).Select(row => (object)new Dictionary<string, object?>
                        {
                            ["projectId"] = id,
                            ["slug"] = TextOf(row, "slug"),
                            ["title"] = TextOf(row, "title"),
                            ["priority"] = TextOf(row, "priority"),
                            ["status"] = TextOf(row, "status"),
                            ["tasks"] = $"{TextOf(row, "taskDone")}/{TextOf(row, "taskTotal")}"
                        }));
                    }
                }
                catch (LoopbackApiException)
                {
                }
            }

            snapshot["queue"] = queue;

            var replies = await api.GetAsync("/api/agent/notifications/replies", ct);
            if (replies.TryGetProperty("items", out var replyItems))
            {
                snapshot["recentReplies"] = replyItems.EnumerateArray().Take(MaxRecentReplies).Select(reply => new Dictionary<string, object?>
                {
                    ["id"] = TextOf(reply, "id"),
                    ["kind"] = TextOf(reply, "kind"),
                    ["text"] = Truncate(TextOf(reply, "text"), 160),
                    ["replyText"] = Truncate(TextOf(reply, "replyText"), 200)
                }).ToList();
            }
        }
        catch (LoopbackApiException)
        {
            snapshot["degraded"] = "the snapshot is incomplete because a board read failed";
        }

        return JsonSerializer.Serialize(snapshot, AgentLoopbackClient.JsonOptions);
    }

    private static IEnumerable<JsonElement> EnumerateJson(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.EnumerateArray();
        }

        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("items", out var items) &&
               items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];
    }

    private static string TextOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? value.GetRawText()
                : string.Empty;

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max] + "…";
}
