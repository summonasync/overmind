using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentPromptComposer(
    AgentLoopbackClient api,
    IAgentRunSettingsProvider settingsProvider,
    IGrimoireMemoryClient memory) : IAgentPromptComposer
{
    public const int MaxHistoryMessages = 24;
    public const int MaxSnapshotProjects = 8;
    public const int MaxQueueRowsPerProject = 10;
    public const int MaxQueueRows = 24;
    public const int MaxRecentReplies = 5;
    public const int MaxMemoryFacts = 5;
    public const string InstructionsHeader = "Owner instructions (standing rules from the human owner):";

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
        AppendOwnerInstructions(builder, settings.Instructions);
        return builder.ToString();
    }

    private static void AppendOwnerInstructions(StringBuilder builder, string instructions)
    {
        var text = instructions.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (text.Length > SettingDefaults.AgentInstructionsMaxLength)
        {
            text = text[..SettingDefaults.AgentInstructionsMaxLength];
        }

        builder.AppendLine();
        builder.AppendLine(InstructionsHeader);
        builder.Append(text);
    }

    private async Task<string> ComposeSnapshotAsync(Guid? projectId, CancellationToken ct)
    {
        var snapshot = new JsonObject();

        try
        {
            var projects = await api.GetAsync("/api/projects", ct);
            var projectIds = new List<Guid>(MaxSnapshotProjects);
            var projectRows = new JsonArray();
            foreach (var project in EnumerateJson(projects).Take(MaxSnapshotProjects))
            {
                projectRows.Add(new JsonObject
                {
                    ["id"] = TextOf(project, "id"),
                    ["name"] = TextOf(project, "name"),
                    ["openFeatures"] = TextOf(project, "openFeatureCount")
                });
                if (Guid.TryParse(TextOf(project, "id"), out var id))
                {
                    projectIds.Add(id);
                }
            }

            snapshot["projects"] = projectRows;

            var queue = new JsonArray();
            foreach (var id in projectIds)
            {
                if (projectId is { } scoped && id != scoped)
                {
                    continue;
                }

                if (queue.Count >= MaxQueueRows)
                {
                    break;
                }

                try
                {
                    var rows = await api.GetAsync($"/api/agent/projects/{id}/planning/task-queue", ct);
                    if (!rows.TryGetProperty("items", out var items))
                    {
                        continue;
                    }

                    foreach (var row in items.EnumerateArray().Take(MaxQueueRowsPerProject))
                    {
                        if (queue.Count >= MaxQueueRows)
                        {
                            break;
                        }

                        queue.Add(new JsonObject
                        {
                            ["projectId"] = id.ToString(),
                            ["slug"] = TextOf(row, "slug"),
                            ["title"] = TextOf(row, "title"),
                            ["priority"] = TextOf(row, "priority"),
                            ["status"] = TextOf(row, "status"),
                            ["tasks"] = $"{TextOf(row, "taskDone")}/{TextOf(row, "taskTotal")}"
                        });
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
                var replyRows = new JsonArray();
                foreach (var reply in replyItems.EnumerateArray().Take(MaxRecentReplies))
                {
                    replyRows.Add(new JsonObject
                    {
                        ["id"] = TextOf(reply, "id"),
                        ["kind"] = TextOf(reply, "kind"),
                        ["text"] = Truncate(TextOf(reply, "text"), 160),
                        ["replyText"] = Truncate(TextOf(reply, "replyText"), 200)
                    });
                }

                snapshot["recentReplies"] = replyRows;
            }
        }
        catch (LoopbackApiException)
        {
            snapshot["degraded"] = "the snapshot is incomplete because a board read failed";
        }

        var factRows = new JsonArray();
        foreach (var (target, source) in new[]
                 {
                     (GrimoireTomeTarget.Personal, "personal"),
                     (GrimoireTomeTarget.Crew, "crew"),
                 })
        {
            if (factRows.Count >= MaxMemoryFacts)
            {
                break;
            }

            try
            {
                var endpoint = await memory.ResolveEndpointAsync(target, ct);
                var facts = await memory.ListTopFactsAsync(endpoint, MaxMemoryFacts, ct);
                var remaining = MaxMemoryFacts - factRows.Count;
                foreach (var fact in facts.Take(remaining))
                {
                    factRows.Add(new JsonObject
                    {
                        ["content"] = Truncate(fact.Content, 240),
                        ["createdAt"] = fact.CreatedAt.ToString("yyyy-MM-dd"),
                        ["source"] = source,
                    });
                }
            }
            catch (GrimoireMemoryException)
            {
            }
        }

        if (factRows.Count > 0)
        {
            snapshot["memoryFacts"] = factRows;
        }

        return snapshot.ToJsonString(AgentLoopbackClient.JsonOptions);
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
