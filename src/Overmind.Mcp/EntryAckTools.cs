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
    [Description("Marks an entry as consumed by the calling session — appends one additive readBy row " +
        "(alias, vendor, at); the body, version and UpdatedAt are never touched, so reads stay pure and " +
        "consumption is visible without editing the owner's document. Re-ack is an idempotent no-op.")]
    public async Task<CallToolResult> AckEntry(
        [Description("Entry id.")] Guid entryId,
        [Description("Optional project id.")] Guid? projectId = null,
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
