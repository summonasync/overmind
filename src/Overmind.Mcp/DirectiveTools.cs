using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class DirectiveTools
{
    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public DirectiveTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "planning_append_directive", Destructive = false, Idempotent = false)]
    [Description("Appends a coordination directive to a feature's open channel — the one write that crosses " +
        "the claim lock: any session may reach a live holder (HOLD / SYNC / YIELD) without taking it over. " +
        "The holder sees openDirectives on planning_current_task and acks via planning_ack_directive. Pick " +
        "this over wave_chat_post when the message belongs to a feature, not a room.")]
    public async Task<CallToolResult> AppendDirective(
        [Description("Target feature slug.")] string slug,
        [Description("Directive text, soft cap 2000 chars: whole up to 2440, beyond rejected with a lost-tail preview.")] string text,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var directive = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{slug}/directives",
            new { text },
            ct), readback: null, ct);

        var id = directive.GetProperty("id").GetString();
        var warningNote = directive.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0
            ? $"\nWarnings: {warnings.GetRawText()}"
            : string.Empty;
        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Directive {id} appended on '{slug}' — the holder sees it on planning_current_task " +
                           "and acknowledges via planning_ack_directive." + warningNote
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_ack_directive", Destructive = false, Idempotent = true)]
    [Description("Marks one open directive seen — the holder's consumption stamp on the append-only channel: " +
        "acknowledged directives leave the openDirectives projections; the ack flips only the stamp, never the " +
        "directive text, artifacts or statuses. Re-acking is an idempotent no-op.")]
    public async Task<CallToolResult> AckDirective(
        [Description("Target feature slug.")] string slug,
        [Description("Directive id from openDirectives (planning_current_task).")] Guid directiveId,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var ack = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{slug}/directives/{directiveId}/ack",
            new { },
            ct), readback: null, ct);

        var acked = ack.GetProperty("ack").GetBoolean();
        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = acked
                        ? $"Directive {directiveId} acknowledged."
                        : $"Directive {directiveId} was already acknowledged — no change."
                }
            ]
        };
    }
}
