using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class EntryAckTools
{
    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;

    public EntryAckTools(OvermindApiClient api, MutationGuard mutations)
    {
        _api = api;
        _mutations = mutations;
    }

    [McpServerTool(Name = "ack_entry", Destructive = false, Idempotent = true)]
    [Description("Marks an entry as consumed by the calling session — the explicit ack of the read workflow: " +
        "appends one additive readBy row (alias, vendor, at) attributed to the agent session. The entry body, " +
        "its version and UpdatedAt are never touched, reads stay pure (list_entries/read_entry record nothing) " +
        "and owner-only body writes are unchanged — consumption becomes visible without editing the owner's " +
        "document. Acking again with the same identity is an idempotent no-op that keeps the first at stamp. " +
        "projectId is required.")]
    public async Task<CallToolResult> AckEntry(
        [Description("Id of the entry to acknowledge — take it from list_entries or read_entry.")] Guid entryId,
        [Description("Id of the project the entry belongs to.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (projectId is null)
        {
            throw new McpException(PlanningToolText.ProjectIdRequired);
        }

        var ack = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/entries/{entryId}/ack",
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
                        ? $"Entry {entryId} acknowledged."
                        : $"Entry {entryId} was already acknowledged — no change."
                }
            ]
        };
    }
}
