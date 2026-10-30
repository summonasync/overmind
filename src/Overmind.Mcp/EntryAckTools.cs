using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class EntryAckTools
{
    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;
    private readonly ProjectResolver _projects;

    public EntryAckTools(OvermindApiClient api, MutationGuard mutations, ProjectResolver projects)
    {
        _api = api;
        _mutations = mutations;
        _projects = projects;
    }

    [McpServerTool(Name = "ack_entry", Destructive = false, Idempotent = true)]
    [Description("Marks an entry as consumed by the calling session — the explicit ack of the read workflow: " +
        "appends one additive readBy row (alias, vendor, at) attributed to the agent session. The entry body, " +
        "its version and UpdatedAt are never touched, reads stay pure (list_entries/read_entry record nothing) " +
        "and owner-only body writes are unchanged — consumption becomes visible without editing the owner's " +
        "document. Acking again with the same identity is an idempotent no-op that keeps the first at stamp. " +
        "projectId is resolved from OVERMIND_PROJECT_ID or the working directory when omitted; an unregistered " +
        "directory returns guidance instead and never auto-creates a project row.")]
    public async Task<CallToolResult> AckEntry(
        [Description("Id of the entry to acknowledge — take it from list_entries or read_entry.")] Guid entryId,
        [Description("Id of the project the entry belongs to; resolved from OVERMIND_PROJECT_ID or the working directory when omitted; an unregistered directory returns guidance instead.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var resolved = await _projects.ResolveAsync(projectId, create: false, "for the entry", ct);

        var ack = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{resolved}/entries/{entryId}/ack",
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
