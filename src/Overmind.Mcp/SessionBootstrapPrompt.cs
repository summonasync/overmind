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
        "Pick the write by cost: line → planning_append_log; one section / one AC item → planning_patch_artifact; whole body → planning_upsert_artifact; several tasks at once → planning_upsert_tasks; a whole ceremony step (task close, gate present) → planning_batch.";

    private const string ReadCostRouting =
        "Pick the read by cost: one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature; a slice of a heavy read (board, queue, entries, snapshot) → fields=<keys> or compact.";

    private const string DependencyRouting =
        "Feature dependencies: planning_list_feature_dependencies reads the graph, planning_add_feature_dependency / planning_remove_feature_dependency edit it; a feature with unclosed blockers stops current_task naming them — clear or work the blockers, never sneak past. Task level: declare blockedBy sibling orders on the task upsert (declarative replace, empty array clears); current_task skips blocked tasks and stops naming the chain when everything left is blocked.";

    private const string QueueRouting =
        "Work queue: what's next = planning_task_queue; the board order IS the queue (priority asc, then position); the human reorders by dragging, the agent moves with planning_move_feature, and crossing bands via planning_set_feature_priority needs the user's explicit approval.";

    private const string ValidationProofRouting =
        "Spec/plan validation needs human proof: overmind_notify(kind=approval, featureSlug) → the human's bell reply → planning_validate; every edit voids the reply. " +
        "One fresh reply may validate the spec and the plan of the same feature in one pass — any edit voids it for both (the UI door needs no proof).";

    private const string NotificationUrlRouting =
        "Notification reach: the overmind_notify response may carry url — quote the url in chat when asking for an approval or reporting a question so the human can click through.";

    private const string CeremonyRouting =
        "Ceremony order: planning_upsert_artifact(kind=spec) → validate with proof (overmind_notify(kind=approval, featureSlug), wait for the human's reply, planning_validate) → planning_upsert_artifact(kind=plan) → validate with proof again → planning_upsert_tasks execution contracts → work loop (planning_current_task, planning_set_status) → result artifact → the human taps the feature done.";

    private const string FallbackText =
        "At session start, list_projects names the candidate projects, then overmind_briefing returns " +
        "the start-of-work picture in one call — guards and gates with full bodies, decisions and the " +
        "latest notes as snippets, and the tombstone list (read_entry for depth). " +
        "Planning loop: planning_list_features for the board, planning_current_task for your next task, obey any STOP line " +
        "(a gated spec or plan is approved by the user in the Overmind UI, never by you), " +
        "and report progress with planning_set_status and planning_append_log. " +
        WriteCostRouting + " " + ReadCostRouting + " " + DependencyRouting + " " + QueueRouting +
        " " + ValidationProofRouting + " " + NotificationUrlRouting + " " + CeremonyRouting +
        " Set OVERMIND_PROJECT_ID to make the active project explicit.";

    private readonly OvermindApiClient _api;

    public SessionBootstrapPrompt(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerPrompt(Name = "session_bootstrap", Title = "Overmind session bootstrap")]
    [Description("Instruction text for session start: overmind_briefing returns the gates-and-guards picture, the planning loop follows.")]
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
            : $"At session start, call overmind_briefing for project {activeProjectId} — one call returns " +
              "the start-of-work picture: guards and gates with full bodies, decisions and the latest notes " +
              "as snippets, and the tombstone list (read_entry for depth). " +
              "Planning loop: planning_list_features for the board, planning_current_task for your next task, " +
              "obey any STOP line (a gated spec or plan is approved by the user in the Overmind UI, never by you), " +
              $"and report progress with planning_set_status and planning_append_log. {WriteCostRouting} {ReadCostRouting} {DependencyRouting} {QueueRouting} {ValidationProofRouting} {NotificationUrlRouting} {CeremonyRouting}";

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
