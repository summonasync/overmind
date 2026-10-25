using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class WaveRoleTools
{
    public const string WorkerRole = "worker";
    public const string MarshalRole = "marshal";

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;

    public WaveRoleTools(OvermindApiClient api, MutationGuard mutations)
    {
        _api = api;
        _mutations = mutations;
    }

    [McpServerTool(Name = "wave_role_set", Destructive = false, Idempotent = true)]
    [Description("Sets the calling session's own wave chat role — self-serve identity, no marshal approval " +
        "needed. 'worker' is the default at registration; 'marshal' is the wave lead who reads the board across " +
        "all lanes and issues move/wait decisions, so take it only when actually marshalling the wave. Anything " +
        "besides 'worker' or 'marshal' fails with a named validation error.")]
    public async Task<CallToolResult> SetRole(
        [Description("Role to take: 'worker' (default) or 'marshal' (wave lead).")] string role,
        CancellationToken ct = default)
    {
        var normalized = role?.Trim();
        if (normalized is not (WorkerRole or MarshalRole))
        {
            throw new McpException(
                $"Unknown wave role '{role}'. Role must be '{WorkerRole}' or '{MarshalRole}'.");
        }

        if (AdapterSession.Id is not { } sessionId)
        {
            throw new McpException(
                "No registered agent session — the adapter could not register with the host at startup, so " +
                "there is no session identity to set a role on. Restart the adapter with the host running.");
        }

        await _mutations.SendAsync(
            () => _api.PostAsync($"/api/agent/sessions/{sessionId}/role", new { role = normalized }, ct),
            readback: null,
            ct);

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Role set to '{normalized}'."
                }
            ]
        };
    }
}
