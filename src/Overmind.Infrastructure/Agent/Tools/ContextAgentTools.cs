using System.Text.Json;
using System.Text.Json.Nodes;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Infrastructure.Agent.Tools;

public static class ContextAgentTools
{
    public const int MaxSnapshotProjects = 8;
    public const int MaxQueueRowsPerProject = 10;
    public const int MaxQueueRows = 24;
    public const int MaxRecentReplies = 5;
    public const int MaxMemoryFacts = 5;

    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api, IGrimoireMemoryClient memory)
    {
        return [SnapshotSpec(api, memory)];
    }

    private static AgentToolSpec SnapshotSpec(AgentLoopbackClient api, IGrimoireMemoryClient memory) => new(
        "get_board_context",
        "Pulls the current board context on demand: projects with open feature counts, the planning task queue, " +
        "the human's recent notification replies and top memory facts. Call it at the start of a turn when board " +
        "state matters and resolve project ids from its result instead of guessing. Optional projectId scopes the " +
        "queue rows to one project. A failed board read marks the result degraded; memory facts silently drop " +
        "when grimoire is unavailable.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":[]}
        """,
        async (args, ct) =>
        {
            var projectId = AgentToolArgs.OptionalGuid(args, "projectId");
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

            return JsonSerializer.SerializeToElement(snapshot, AgentLoopbackClient.JsonOptions);
        });

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
