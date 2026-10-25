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
    [Description("Files an issue on the board of project projectId — the cross-project protocol: working in project A and need a fix in project B (this board)? File an issue here and do not touch their repo; follow-ups = issues_append; the owner team triages/promotes. Optional kind: 'issue' (default) or 'request' — a bug report vs a feature request; both carry authorProjectId as provenance (validated against existing projects, not permission) and both promote into tasks the same way; anything besides those two values fails with a named validation error. Body recommendation: '## Repro' / '## Expected' / '## Actual' sections; free-form accepted. A foreign create (authorProjectId differs from the board) posts a notification-center entry on the owner project with a deep link — the owner team hears about it without polling.")]
    public async Task<CallToolResult> Create(
        [Description("Id of the project whose board receives the issue.")] Guid projectId,
        [Description("Id of the project filing the issue (provenance; must exist).")] Guid authorProjectId,
        [Description("Short issue title.")] string title,
        [Description("Issue body markdown; recommended sections '## Repro', '## Expected', '## Actual'.")] string body,
        [Description("Kind of card to file: 'issue' (default) or 'request'. Both promote into tasks the same way.")] string? kind = null,
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
    [Description("Lists issues on the board of project projectId — metadata rows only (no body, no append texts): id, title, status (open/triaged/promoted/closed), authorProjectId, createdAt, closedAt, promotedToArtifactId, appendCount, kind. Optional filters: status, authorProjectId and kind (issue | request). Reads are open to any project — provenance, not permission.")]
    public async Task<CallToolResult> List(
        [Description("Id of the project whose board to read.")] Guid projectId,
        [Description("Filter by status: open, triaged, promoted or closed. Omit for all.")] string? status = null,
        [Description("Filter by filing project id. Omit for all.")] Guid? authorProjectId = null,
        [Description("Filter by kind: 'issue' or 'request'. Omit for all.")] string? kind = null,
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
    [Description("Reads one issue in full: card (title, body, status, author provenance, lifecycle timestamps) plus the chronological append trail. Reads are open to any project.")]
    public async Task<CallToolResult> Get(
        [Description("Id of the issue to read.")] Guid issueId,
        CancellationToken ct = default)
    {
        var card = await Guarded(() => _api.GetAsync(
            $"/api/agent/issues/{issueId}", ct));
        return EchoResult(card);
    }

    [McpServerTool(Name = "issues_append", Destructive = false, Idempotent = false)]
    [Description("Appends a follow-up to an issue — the cross-project protocol for new information: entries land in the append-only trail with authorProjectId + text + createdAt and are never edited or deleted; no duplicate issues for follow-ups. Works from any project; the trail moves into the task's <obstacles> when the issue is promoted.")]
    public async Task<CallToolResult> Append(
        [Description("Id of the issue to append to.")] Guid issueId,
        [Description("Id of the project appending the follow-up (provenance; must exist).")] Guid authorProjectId,
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
        return EchoResult(compact.Deserialize<JsonElement>());
    }

    [McpServerTool(Name = "issues_set_status", Destructive = false, Idempotent = true)]
    [Description("Moves an issue through its lifecycle: open → triaged → promoted → closed. Closed is reachable from any non-closed state (not-a-bug close — put the reason in issues_append text); there is no reopen, a re-found bug is a new issue. Promoted is reached only through issues_promote and is refused here. Every move records the source marker (agent/ui) as its provenance — issues have no log, the marker is the provenance.")]
    public async Task<CallToolResult> SetStatus(
        [Description("Id of the issue to move.")] Guid issueId,
        [Description("New status: triaged or closed. ('promoted' is refused — use issues_promote.)")] string status,
        [Description("Who moves it: Agent or Ui.")] string source,
        [Description("Accepted for protocol symmetry; issues have no log — the source marker is the provenance. Unused.")] string? logNote = null,
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
    [Description("Machine promotion, one call — per crew rule 10 an issue or a request is a ready-made execution contract: composes a task artifact on featureSlug (goal from title+body, scope from the Repro/Expected/Actual sections, the append trail into <obstacles>, <refs> pointing at the issue) and sets the issue to promoted with a link to the artifact — traceable both ways. Only from open/triaged; promotion belongs to the board's team. A claimed target feature blocks promotion from any session that does not hold the claim — in any liveness state (promote is not take-over): resolve the claim with the holder or wait for the owner team. The append trail moves into <obstacles> verbatim.")]
    public async Task<CallToolResult> Promote(
        [Description("Id of the issue to promote.")] Guid issueId,
        [Description("Slug of the feature on this board that receives the composed task.")] string featureSlug,
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
