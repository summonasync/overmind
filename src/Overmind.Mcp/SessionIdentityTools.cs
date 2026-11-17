using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class SessionIdentityTools(OvermindApiClient api, SessionIdentityFileWriter identityFile)
{
    public const int MaxIdentityLength = 32;

    [McpServerTool(Name = "session_identity_set", Destructive = false, Idempotent = true)]
    [Description("Sets the calling session's own identity (alias + vendor) on the host — session-scoped; " +
        "set it BEFORE claiming a feature, never from a sub-agent of a live session. A 409 means another " +
        "alive session holds the alias — invent another and retry. Success pins the pair as the adapter's " +
        "reconnect identity.")]
    public async Task<CallToolResult> SetIdentity(
        [Description("Alias, 1-32 chars, per the project naming rules.")] string alias,
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
        var registeredAlias = session.GetProperty("alias").GetString() ?? normalizedAlias;
        var registeredVendor = session.GetProperty("vendor").GetString() ?? normalizedVendor;
        AdapterIdentityState.Set(registeredAlias, registeredVendor);
        identityFile.Write(registeredAlias, registeredVendor, session.GetProperty("id").GetGuid());

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
