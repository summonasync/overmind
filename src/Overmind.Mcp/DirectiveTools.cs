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
        [Description("Directive id, or a unique hex prefix of one (\"477cec39\").")] string directiveId,
        [Description("Optional project id.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the working directory", ct);
        var target = await ResolveDirectiveIdAsync(resolved, slug, directiveId, ct);
        var ack = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/planning/features/{slug}/directives/{target}/ack",
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
                        ? $"Directive {target} acknowledged."
                        : $"Directive {target} was already acknowledged — no change."
                }
            ]
        };
    }

    // 266/T3 row 6, the prefix ruling: relays and wave snapshots expose short ids, and every ack
    // after a relay used to bounce ("477cec39" is not a UUID), costing a re-read of the feature
    // plus a resend. Accepting a UNIQUE hex prefix kills the round-trip; the uniqueness check is
    // mandatory, and an ambiguous prefix is refused with the candidates spelled out. A full UUID
    // is normalised exactly as before, so the URL a full id produces is byte-identical.
    private async Task<string> ResolveDirectiveIdAsync(Guid projectId, string slug, string raw, CancellationToken ct)
    {
        var token = raw.Trim();
        if (Guid.TryParse(token, out var exact))
        {
            return exact.ToString("D");
        }

        if (token.Length is 0 or > 31 || !token.All(char.IsAsciiHexDigit))
        {
            throw new McpException(
                $"directiveId '{raw}' is neither a UUID nor a hex prefix — a prefix is 1-31 hex digits " +
                "taken from an id the relay or snapshot showed you.");
        }

        var current = await _api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/current", ct);
        var open = OpenDirectiveIds(current);
        var hits = open
            .Where(id => id.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (hits.Length == 1)
        {
            return hits[0];
        }

        if (hits.Length > 1)
        {
            throw new McpException(
                $"'{token}' is the prefix of {hits.Length} open directives on '{slug}' — ambiguous, " +
                $"nothing was acknowledged. Candidates: {string.Join(", ", hits)}.");
        }

        throw new McpException(
            $"no open directive on '{slug}' starts with '{token}'. " +
            (open.Count == 0
                ? "The feature has no open directives — an acknowledged one leaves this projection, " +
                  "so the ack you are retrying probably already landed."
                : $"Open ids: {string.Join(", ", open)}.") +
            " Nothing was acknowledged.");
    }

    private static IReadOnlyList<string> OpenDirectiveIds(JsonElement current)
    {
        if (!current.TryGetProperty("openDirectives", out var directives)
            || directives.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. directives.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object
                && row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(row => row.GetProperty("id").GetString()!)];
    }
}
