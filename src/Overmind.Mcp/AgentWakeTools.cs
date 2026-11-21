using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class AgentWakeTools(OvermindApiClient api, ProjectResolver projects)
{
    [McpServerTool(Name = "overmind_wake_peek", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Wake ritual: a side-effect-free peek at pending signals — human replies " +
        "to this session's notifications (kind 'notification-reply') and wave chat mentions of its " +
        "alias (kind 'wave-mention'). Advances nothing; a reply leaves the default window once " +
        "overmind_replies delivered it on a final page, an explicit since replays consumed " +
        "traffic. Contract: docs/mcp-tool-contracts.md#overmind_wake_peek.")]
    public async Task<JsonElement> Peek(
        [Description("Optional ISO-8601 timestamp or the cursor echoed by the previous call, verbatim; " +
            "omit for the endpoint defaults.")] string? since = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(since))
        {
            query.Add($"since={Uri.EscapeDataString(since.Trim())}");
        }

        if (await ResolvedProjectIdAsync(ct) is { } projectId)
        {
            query.Add($"projectId={projectId}");
        }

        var url = query.Count == 0
            ? "/api/agent/wake"
            : $"/api/agent/wake?{string.Join("&", query)}";
        try
        {
            return await api.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private async Task<Guid?> ResolvedProjectIdAsync(CancellationToken ct)
    {
        try
        {
            return await projects.ResolveAsync(null, create: false, "for the wake poll", ct);
        }
        catch (McpException)
        {
            return null;
        }
    }
}
