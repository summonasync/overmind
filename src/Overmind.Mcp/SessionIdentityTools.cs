using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class SessionIdentityTools(OvermindApiClient api)
{
    public const int MaxIdentityLength = 32;

    [McpServerTool(Name = "session_identity_set", Destructive = false, Idempotent = true)]
    [Description("Sets the calling session's own identity (alias + vendor) on the host — self-serve like " +
        "wave_role_set, session-scoped, calling-session-only. Orchestrator-only convention: sub-agents of a " +
        "live session never call it — one shared session row, last-write-wins; set identity BEFORE claiming " +
        "a feature. The alias is invented client-side per the project's naming rules (see overmind_onboard's " +
        "agentIdentity block). A 409 means another alive session holds the alias — the error names the holder, " +
        "invent another name and retry. A success echoes the stored pair plus warnings and pins the pair as the " +
        "adapter's reconnect identity: handshakes re-register under it instead of OVERMIND_ALIAS/OVERMIND_VENDOR.")]
    public async Task<CallToolResult> SetIdentity(
        [Description("New alias, 1-32 chars after trim; invented client-side per project naming rules.")] string alias,
        [Description("New vendor, 1-32 chars after trim (e.g. 'claude', 'qwen').")] string vendor,
        CancellationToken ct = default)
    {
        var normalizedAlias = Normalize(alias, "alias");
        var normalizedVendor = Normalize(vendor, "vendor");

        if (AdapterSession.Id is not { } sessionId)
        {
            throw new McpException(
                "No registered agent session — the adapter could not register with the host at startup, so " +
                "there is no session identity to set. Restart the adapter with the host running.");
        }

        JsonElement response;
        try
        {
            response = await api.PostAsync(
                $"/api/agent/sessions/{sessionId}/identity",
                new { alias = normalizedAlias, vendor = normalizedVendor },
                ct);
        }
        catch (ApiStatusException ex) when (ex.StatusCode == (int)HttpStatusCode.Conflict)
        {
            throw new McpException(DetailOf(ex) + " Invent another alias and retry session_identity_set.");
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(DetailOf(ex));
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }

        var session = response.GetProperty("session");
        AdapterIdentityState.Set(
            session.GetProperty("alias").GetString() ?? normalizedAlias,
            session.GetProperty("vendor").GetString() ?? normalizedVendor);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = Echo(session, response) }]
        };
    }

    private static string Normalize(string? value, string parameter)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new McpException($"'{parameter}' is required and must not be blank.");
        }

        if (trimmed.Length > MaxIdentityLength)
        {
            throw new McpException(
                $"'{parameter}' exceeds the {MaxIdentityLength}-char identity limit ({trimmed.Length} chars).");
        }

        return trimmed;
    }

    private static string Echo(JsonElement session, JsonElement response)
    {
        var text = new StringBuilder();
        text.Append($"Identity set to '{session.GetProperty("alias").GetString()}' by " +
                    $"'{session.GetProperty("vendor").GetString()}' (session {session.GetProperty("id").GetGuid()}).");

        var warnings = response.GetProperty("warnings");
        if (warnings.GetArrayLength() > 0)
        {
            text.Append("\nWarnings:");
            foreach (var warning in warnings.EnumerateArray())
            {
                text.Append("\n- ").Append(warning.GetString());
            }
        }

        return text.ToString();
    }

    private static string DetailOf(ApiStatusException ex) =>
        MutationGuard.DetailAfterOperationMark(ex.Message) ?? ex.Message;
}
