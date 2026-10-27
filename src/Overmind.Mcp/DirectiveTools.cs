using System.ComponentModel;
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
    [Description("Appends a coordination directive to a feature's open directive channel — the one write that " +
        "crosses the claim lock. Any session may append to ANY feature regardless of claim ownership or feature " +
        "status (HOLD / SYNC / YIELD from a wave marshal or lane neighbor reaches a live holder without taking " +
        "over the claim). Directives are append-only and attributed: text is trimmed, capped at 2000 chars, and " +
        "stamped with the sender's session identity. Nothing else is touched — no artifacts, no statuses, no " +
        "version bumps, the 409 claim lock itself stays intact. The holder sees open directives on " +
        "planning_current_task and planning_read_feature payloads (openDirectives, oldest first) and on queue " +
        "rows as openDirectiveCount; the holder acknowledges with planning_ack_directive.")]
    public async Task<CallToolResult> AppendDirective(
        [Description("Slug of the feature the directive targets.")] string slug,
        [Description("Directive text, e.g. 'HOLD: proxy.ts is shared with 134 — sync before the next push'. " +
            "Trimmed, max 2000 chars.")] string text,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var directive = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{slug}/directives",
            new { text },
            ct), readback: null, ct);

        var id = directive.GetProperty("id").GetString();
        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Directive {id} appended on '{slug}' — the holder sees it on planning_current_task " +
                           "and acknowledges via planning_ack_directive."
                }
            ]
        };
    }

    [McpServerTool(Name = "planning_ack_directive", Destructive = false, Idempotent = true)]
    [Description("Acknowledges one open directive on a feature — the holder (or any session) marks a directive " +
        "as seen. Acknowledged directives leave the openDirectives projections and the openDirectiveCount drops; " +
        "the ack is attributed to the calling session. Acking an already-acked directive is an idempotent no-op " +
        "success. Append-only channel: an ack flips only the ack stamp, never the directive text, the artifacts, " +
        "statuses or versions.")]
    public async Task<CallToolResult> AckDirective(
        [Description("Slug of the feature carrying the directive.")] string slug,
        [Description("Id of the directive to acknowledge — take it from openDirectives on planning_current_task " +
            "or planning_read_feature.")] Guid directiveId,
        [Description("Id of the project the feature belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
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
