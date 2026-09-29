using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindNotificationTools
{
    private const string ProjectIdEnvironmentVariable = "OVERMIND_PROJECT_ID";

    private static readonly HashSet<string> NotificationKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "question", "approval"
    };

    private readonly OvermindApiClient _api;

    public OvermindNotificationTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "overmind_notify", Destructive = false, Idempotent = false)]
    [Description("Tell the human something: posts a notification to the center the human watches in the Overmind web UI. Kind is info, question or approval; featureSlug and tab turn it into a deep link onto the feature page. Use question or approval when a gate needs the human's call, then pick the answer up with overmind_replies. The response may carry url (set when featureSlug is) — quote the url in chat when asking for an approval or reporting a question so the human can click through.")]
    public async Task<JsonElement> Notify(
        [Description("Notification kind: info, question or approval.")] string kind,
        [Description("Message body shown to the human, max 2000 chars.")] string text,
        [Description("Id of the project the notification targets; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description("Slug of the feature the deep link opens, as shown on the planning board.")] string? featureSlug = null,
        [Description("Feature page tab the deep link opens: spec, plan, tasks or result.")] string? tab = null,
        [Description("Sender name shown next to the notification in the center; defaults to 'agent'.")] string? from = null,
        CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["kind"] = NormalizeKind(kind),
            ["text"] = text
        };

        payload["projectId"] = await ResolveProjectIdAsync(projectId, ct);
        if (featureSlug is not null)
        {
            payload["featureSlug"] = featureSlug;
        }

        if (tab is not null)
        {
            payload["tab"] = tab;
        }

        if (from is not null)
        {
            payload["from"] = from;
        }

        return await Guarded(() => _api.PostAsync("/api/agent/notifications", payload, ct));
    }

    [McpServerTool(Name = "overmind_replies", ReadOnly = true, Idempotent = true)]
    [Description("Poll for the human's answers: notifications you sent via overmind_notify that carry a reply, newest first. Poll it after asking a question or requesting approval — each item's replyText is the human's decision.")]
    public async Task<JsonElement> Replies(
        [Description("Id of the project whose replies to read; resolved from OVERMIND_PROJECT_ID or the working directory when omitted.")] Guid? projectId = null,
        [Description("A reply marks its notification read, so false matches nothing — poll with the default true.")] bool? includeRead = null,
        CancellationToken ct = default)
    {
        var resolved = await ResolveProjectIdAsync(projectId, ct);
        var query = includeRead is null
            ? $"projectId={resolved}"
            : $"projectId={resolved}&includeRead={(includeRead.Value ? "true" : "false")}";
        return await Guarded(() => _api.GetAsync($"/api/agent/notifications/replies?{query}", ct));
    }

    private async Task<Guid> ResolveProjectIdAsync(Guid? projectId, CancellationToken ct)
    {
        if (projectId is not null)
        {
            return projectId.Value;
        }

        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        if (Guid.TryParse(overrideId, out var fromEnvironment))
        {
            return fromEnvironment;
        }

        JsonElement onboard;
        try
        {
            onboard = await _api.PostAsync("/api/onboard", new { cwd = Environment.CurrentDirectory }, ct);
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(
                $"Could not resolve the project for the notification: {ex.Message}. Pass projectId explicitly or call overmind_onboard first.");
        }

        if (onboard.ValueKind == JsonValueKind.Object &&
            onboard.TryGetProperty("projectId", out var value) &&
            value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out var resolved))
        {
            return resolved;
        }

        throw new McpException(
            "The host returned no usable projectId for the working directory. Call overmind_onboard, then pass its projectId explicitly.");
    }

    private static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static string NormalizeKind(string kind)
    {
        var trimmed = kind.Trim();
        if (!NotificationKinds.Contains(trimmed))
        {
            throw new McpException($"Invalid notification kind '{kind}'. Expected one of: info, question, approval.");
        }

        return trimmed.ToLowerInvariant();
    }
}
