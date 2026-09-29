using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerPromptType]
public sealed class SessionBootstrapPrompt
{
    private const string WriteCostRouting =
        "Pick the write by cost: line → planning_append_log; one section / one AC item → planning_patch_artifact; whole body → planning_upsert_artifact; several tasks at once → planning_upsert_tasks.";

    private const string ReadCostRouting =
        "Pick the read by cost: one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature.";

    private const string DependencyRouting =
        "Feature dependencies: planning_list_feature_dependencies reads the graph, planning_add_feature_dependency / planning_remove_feature_dependency edit it; a feature with unclosed blockers stops current_task naming them — clear or work the blockers, never sneak past. Task level: declare blockedBy sibling orders on the task upsert (declarative replace, empty array clears); current_task skips blocked tasks and stops naming the chain when everything left is blocked.";

    private const string QueueRouting =
        "Work queue: what's next = planning_task_queue; the board order IS the queue (priority asc, then position); the human reorders by dragging, the agent moves with planning_move_feature, and crossing bands via planning_set_feature_priority needs the user's explicit approval.";

    private const string ValidationProofRouting =
        "Spec/plan validation needs human proof: overmind_notify(kind=approval, featureSlug) → the human's bell reply → planning_validate; every edit voids the reply (the UI door needs no proof).";

    private const string NotificationUrlRouting =
        "Notification reach: the overmind_notify response may carry url — quote the url in chat when asking for an approval or reporting a question so the human can click through.";

    private const string FallbackText =
        "At session start, read overmind://projects to list available projects, then read " +
        "overmind://projects/{id}/gates and /guards for the active project and follow them. " +
        "Planning loop: read overmind://projects/{id}/planning/features for the board, take " +
        "overmind://projects/{id}/planning/features/<slug>/current for your next task, obey any STOP line " +
        "(a gated spec or plan is approved by the user in the Overmind UI, never by you), " +
        "and report progress with planning_set_status and planning_append_log. " +
        WriteCostRouting + " " + ReadCostRouting + " " + DependencyRouting + " " + QueueRouting +
        " " + ValidationProofRouting + " " + NotificationUrlRouting +
        " Set OVERMIND_PROJECT_ID to make the active project explicit.";

    private readonly OvermindApiClient _api;

    public SessionBootstrapPrompt(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerPrompt(Name = "session_bootstrap", Title = "Overmind session bootstrap")]
    [Description("Instruction text telling the agent which gates and guards to read at session start.")]
    public async Task<GetPromptResult> GetBootstrap(CancellationToken ct)
    {
        var activeProjectId = Environment.GetEnvironmentVariable("OVERMIND_PROJECT_ID");

        if (string.IsNullOrWhiteSpace(activeProjectId))
        {
            JsonElement projects;
            try
            {
                projects = await _api.GetAsync("/api/projects", ct);
            }
            catch (Exception ex) when (ex is ApiUnreachableException or ApiStatusException)
            {
                throw new McpException(ex.Message);
            }

            var list = projects.EnumerateArray().ToArray();
            if (list.Length == 1)
            {
                activeProjectId = list[0].GetProperty("id").GetString();
            }
        }

        var text = string.IsNullOrWhiteSpace(activeProjectId)
            ? FallbackText
            : $"At session start, read overmind://projects/{activeProjectId}/gates and /guards and follow them. " +
              $"Planning loop: read overmind://projects/{activeProjectId}/planning/features for the board, " +
              $"take overmind://projects/{activeProjectId}/planning/features/<slug>/current for your next task, " +
              "obey any STOP line (a gated spec or plan is approved by the user in the Overmind UI, never by you), " +
              $"and report progress with planning_set_status and planning_append_log. {WriteCostRouting} {ReadCostRouting} {DependencyRouting} {QueueRouting} {ValidationProofRouting} {NotificationUrlRouting}";

        return new GetPromptResult
        {
            Description = "Overmind session bootstrap instructions",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock { Text = text }
                }
            ]
        };
    }
}
