using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class IssuesTools
{
    private const string BodyTemplateRecommendation =
        "Recommended body sections: '## Repro', '## Expected', '## Actual' — free-form markdown is accepted.";

    private readonly OvermindApiClient _api;
    private readonly MutationGuard _mutations;

    public IssuesTools(OvermindApiClient api, MutationGuard mutations)
    {
        _api = api;
        _mutations = mutations;
    }

    [McpServerTool(Name = "issues_create", Destructive = false, Idempotent = false)]
    [Description("Files an issue (or kind='request' card) on another project's board — the cross-project " +
        "protocol: need a fix in project B? File here, do not touch their repo. Follow-ups go to " +
        "issues_append, never a duplicate card; a foreign create notifies the owner board.")]
    public async Task<CallToolResult> Create(
        [Description("Project id of the receiving board.")] Guid projectId,
        [Description("Filing project id (provenance; must exist).")] Guid authorProjectId,
        [Description("Short issue title.")] string title,
        [Description("Issue markdown; '## Repro'/'## Expected'/'## Actual' recommended.")] string body,
        [Description("'issue' (default) or 'request'.")] string? kind = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new McpException("title is required and cannot be blank.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new McpException(
                "body is required — a bug report without a description helps nobody. " + BodyTemplateRecommendation);
        }

        var payload = new JsonObject
        {
            ["authorProjectId"] = authorProjectId,
            ["title"] = title.Trim(),
            ["body"] = body.Trim()
        };
        var normalizedKind = NormalizeKind(kind);
        if (normalizedKind is not null)
        {
            payload["kind"] = normalizedKind;
        }

        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/projects/{projectId}/issues",
            payload,
            ct), readback: null, ct);

        var compact = new JsonObject
        {
            ["id"] = echo.GetProperty("id").GetString(),
            ["status"] = echo.GetProperty("status").GetString(),
            ["title"] = echo.GetProperty("title").GetString(),
            ["kind"] = echo.GetProperty("kind").GetString()
        };
        return EchoResult(compact.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "issues_list", ReadOnly = true, Idempotent = true)]
    [Description("Lists issue metadata rows on a board — no bodies, no append texts; issues_get reads one " +
        "in full. Reads are open to any project — provenance, not permission.")]
    public async Task<CallToolResult> List(
        [Description("Project board to read.")] Guid projectId,
        [Description("Status filter: open, triaged, promoted, closed.")] string? status = null,
        [Description("Filing project filter.")] Guid? authorProjectId = null,
        [Description("Kind filter: 'issue' or 'request'.")] string? kind = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        var trimmedStatus = Trimmed(status);
        if (trimmedStatus is not null)
        {
            query.Add("status=" + Uri.EscapeDataString(trimmedStatus));
        }

        if (authorProjectId.HasValue)
        {
            query.Add("authorProjectId=" + authorProjectId.Value);
        }

        var normalizedKind = NormalizeKind(kind);
        if (normalizedKind is not null)
        {
            query.Add("kind=" + Uri.EscapeDataString(normalizedKind));
        }

        var path = $"/api/agent/projects/{projectId}/issues";
        if (query.Count > 0)
        {
            path += "?" + string.Join("&", query);
        }

        var rows = await Guarded(() => _api.GetAsync(path, ct));
        return EchoResult(rows);
    }

    [McpServerTool(Name = "issues_get", ReadOnly = true, Idempotent = true)]
    [Description("Reads one issue in full — card plus the chronological append trail; open to any project.")]
    public async Task<CallToolResult> Get(
        [Description("Issue id.")] Guid issueId,
        CancellationToken ct = default)
    {
        var card = await Guarded(() => _api.GetAsync(
            $"/api/agent/issues/{issueId}", ct));
        return EchoResult(card);
    }

    [McpServerTool(Name = "issues_append", Destructive = false, Idempotent = false)]
    [Description("Appends a follow-up to an issue — the trail is append-only (author, text, createdAt; " +
        "never edited or deleted), so never file a duplicate issue for new information. Works from any " +
        "project; the trail moves into the task's <obstacles> on promote.")]
    public async Task<CallToolResult> Append(
        [Description("Issue id.")] Guid issueId,
        [Description("Filing project id (provenance; must exist).")] Guid authorProjectId,
        [Description("Follow-up text.")] string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new McpException("text is required and cannot be blank.");
        }

        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/issues/{issueId}/append",
            new { authorProjectId, text = text.Trim() },
            ct), readback: null, ct);

        var compact = new JsonObject
        {
            ["id"] = echo.GetProperty("id").GetString(),
            ["authorProjectId"] = echo.GetProperty("authorProjectId").GetString(),
            ["text"] = echo.GetProperty("text").GetString(),
            ["createdAt"] = echo.GetProperty("createdAt").GetString()
        };
        if (echo.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0)
        {
            compact["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }

        return EchoResult(compact.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "issues_set_status", Destructive = false, Idempotent = true)]
    [Description("Moves an issue through open → triaged → promoted → closed. Settable here: triaged and " +
        "closed; promoted comes from issues_promote only. Closed is reachable from any non-closed state as " +
        "the not-a-bug close — put the reason in issues_append. No reopen: a re-found bug is a new issue.")]
    public async Task<CallToolResult> SetStatus(
        [Description("Issue id.")] Guid issueId,
        [Description("triaged or closed ('promoted' needs issues_promote).")] string status,
        [Description("Who moves it: Agent or Ui.")] string source,
        [Description("Unused — issues have no log.")] string? logNote = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeStatus(status);
        var normalizedSource = NormalizeSource(source);

        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/issues/{issueId}/status",
            new { status = normalizedStatus, source = normalizedSource },
            ct), readback: null, ct);

        var compact = new JsonObject
        {
            ["id"] = echo.GetProperty("id").GetString(),
            ["status"] = echo.GetProperty("status").GetString(),
            ["source"] = echo.GetProperty("source").GetString(),
            ["closedAt"] = echo.GetProperty("closedAt").GetString()
        };
        return EchoResult(compact.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "issues_promote", Destructive = false, Idempotent = false)]
    [Description("Promotes an issue or request into a task artifact on the target feature, linking card " +
        "and artifact both ways. Only from open/triaged, for the board's own team — a claimed target " +
        "feature blocks promotion from sessions without the claim.")]
    public async Task<CallToolResult> Promote(
        [Description("Issue id.")] Guid issueId,
        [Description("Feature slug receiving the composed task.")] string featureSlug,
        [Description("Who promotes it: Agent or Ui.")] string source,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(featureSlug))
        {
            throw new McpException("featureSlug is required — issues promote into tasks on an existing feature of this board.");
        }

        var echo = await _mutations.SendAsync(() => _api.PostAsync(
            $"/api/agent/issues/{issueId}/promote",
            new { featureSlug = featureSlug.Trim(), source = NormalizeSource(source) },
            ct), readback: null, ct);

        var compact = new JsonObject
        {
            ["id"] = echo.GetProperty("id").GetString(),
            ["status"] = echo.GetProperty("status").GetString(),
            ["artifactId"] = echo.GetProperty("artifactId").GetString(),
            ["featureId"] = echo.GetProperty("featureId").GetString(),
            ["artifactVersion"] = echo.GetProperty("artifactVersion").GetInt32()
        };
        return EchoResult(compact.Deserialize<JsonElement>());
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeKind(string? kind)
    {
        var trimmed = Trimmed(kind);
        if (trimmed is null)
        {
            return null;
        }

        if (trimmed.Equals("issue", StringComparison.OrdinalIgnoreCase))
        {
            return "issue";
        }

        if (trimmed.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return "request";
        }

        throw new McpException($"Invalid kind '{kind}'. Expected one of: issue, request.");
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new McpException("status is required and cannot be blank.");
        }

        var trimmed = status.Trim();
        if (trimmed.Equals("triaged", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
        }

        throw new McpException(
            $"Invalid issue status '{status}'. Expected one of: triaged, closed. " +
            "('promoted' is refused — use issues_promote.)");
    }

    private static string NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new McpException("source is required and cannot be blank.");
        }

        var trimmed = source.Trim();
        if (trimmed.Equals("agent", StringComparison.OrdinalIgnoreCase)) return "Agent";
        if (trimmed.Equals("ui", StringComparison.OrdinalIgnoreCase)) return "Ui";
        throw new McpException($"Invalid source '{source}'. Expected one of: Agent, Ui.");
    }

    private async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static CallToolResult EchoResult(JsonElement echo) =>
        new() { Content = [new TextContentBlock { Text = echo.GetRawText() }] };
}
