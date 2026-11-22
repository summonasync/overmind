using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

internal static class PlanningToolResponses
{
    private static readonly HashSet<string> ArtifactKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "spec", "plan", "task", "result"
    };

    private static readonly HashSet<string> ReviewLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "light", "hard"
    };

    private static readonly Dictionary<string, string> ArtifactStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["draft"] = "draft",
        ["validated"] = "validated",
        ["todo"] = "todo",
        ["inProgress"] = "inProgress",
        ["review"] = "review",
        ["done"] = "done",
        ["blocked"] = "blocked"
    };

    private static readonly Dictionary<string, string> AgentFeatureStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["done"] = "done",
        ["inProgress"] = "inProgress"
    };

    private static readonly HashSet<string> FeaturePriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "p0", "p1", "p2", "p3"
    };

    private static readonly HashSet<string> QueueMoves = new(StringComparer.OrdinalIgnoreCase)
    {
        "up", "down", "first", "last", "toPosition"
    };

    internal const string StatusNoteRequired =
        "logNote is required for status moves: every status change is recorded as a dated line in the artifact <log>.";

    internal const string FeatureStatusNoteRequired =
        "logNote is required for feature status moves: the note is recorded as a dated line in the feature log and carries the provenance (who asked for the move).";

    internal const string FeaturePatchNoteRequired =
        "logNote is required for feature patches: the note is recorded as a dated line in the feature log — why the section changed.";

    internal const string PatchShapeError =
        "A patch carries exactly one of: a section (content optional — a missing section is inserted, declared-empty when content is omitted), an acceptance-criteria tick or un-tick (acIndex or exact acText), or a task brief — never several and never none.";

    internal const string MigratedNoteRequired =
        "logNote is required with asMigrated: the migration marker must record where the pre-approval came from.";

    internal const string OperationIdDescription =
        "Optional idempotency GUID for a replay-safe retry.";

    internal const string TaskItemsAcceptedForm =
        "tasks must be a JSON array of task items, each an object with a required body string — " +
        "e.g. tasks=[{\"body\":\"<goal>Fix the login redirect.</goal>\",\"order\":0}].";

    internal static async Task<JsonElement> Guarded(Func<Task<JsonElement>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiStatusException ex)
        {
            throw new McpException(MutationGuard.StopDetailOf(ex) ?? ex.Message);
        }
        catch (Exception ex) when (ex is ApiNotFoundException or ApiUnreachableException)
        {
            throw new McpException(ex.Message);
        }
    }

    internal static MutationReadback ArtifactLogReadback(Guid id, string marker) =>
        new($"/api/agent/planning/artifacts/{id}?sections=log", marker, $"artifact {id}");

    internal static MutationReadback FeatureLogReadback(Guid projectId, string slug, string marker) =>
        new(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}?includeArtifacts=false",
            marker,
            $"feature '{slug}'");

    internal static MutationReadback? PatchReadback(Guid id, string? logNote) =>
        string.IsNullOrWhiteSpace(logNote) ? null : ArtifactLogReadback(id, logNote);

    internal static MutationReadback? ValidateReadback(Guid artifactId, string? logNote) =>
        string.IsNullOrWhiteSpace(logNote) ? null : ArtifactLogReadback(artifactId, logNote);

    internal static CallToolResult ContractViolation(string text) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = text }]
    };

    internal static string? QueueMoveContractError(string? move, int? position)
    {
        if (string.IsNullOrWhiteSpace(move))
        {
            return "move is required: one of up, down, first, last, toPosition. position carries the 1-based spot and belongs to toPosition alone.";
        }

        var gesture = move.Trim();
        if (!QueueMoves.Contains(gesture))
        {
            return $"Invalid queue move '{move}'. Expected one of: up, down, first, last, toPosition. " +
                "position carries the 1-based spot in the priority band and belongs to toPosition alone.";
        }

        if (string.Equals(gesture, "toPosition", StringComparison.OrdinalIgnoreCase))
        {
            return position switch
            {
                null => "position is required when move is 'toPosition': the 1-based spot in the feature's priority band. " +
                    "The other moves resolve the spot themselves.",
                < 1 => $"position must be a positive int (1-based), got {position}.",
                _ => null
            };
        }

        return position is null
            ? null
            : "position is only allowed when move is 'toPosition'; omit it for up, down, first and last — " +
                "the server swaps with the adjacent non-done feature or jumps to the band edges itself.";
    }

    internal static void CarryReplayedFlag(JsonObject echo, JsonElement payload)
    {
        if (payload.TryGetProperty("replayed", out var replayed) && replayed.ValueKind == JsonValueKind.True)
        {
            echo["replayed"] = true;
        }
    }

    internal static JsonElement MutationEcho(JsonElement payload, string idKey)
    {
        var echo = new JsonObject
        {
            ["id"] = payload.GetProperty(idKey).GetString(),
            ["version"] = payload.GetProperty("version").GetInt32(),
            ["status"] = payload.GetProperty("status").GetString(),
            ["lastLogLine"] = payload.GetProperty("lastLogLine").GetString()
        };

        if (payload.TryGetProperty("warnings", out var warnings) &&
            warnings.ValueKind == JsonValueKind.Array &&
            warnings.GetArrayLength() > 0)
        {
            echo["warnings"] = JsonNode.Parse(warnings.GetRawText());
        }

        if (payload.TryGetProperty("featureStatusChanged", out var featureStatusChanged) &&
            featureStatusChanged.ValueKind == JsonValueKind.String)
        {
            echo["featureStatusChanged"] = featureStatusChanged.GetString();
        }

        CarryReplayedFlag(echo, payload);
        return echo.Deserialize<JsonElement>();
    }

    internal static CallToolResult EchoResult(JsonElement echo) =>
        new() { Content = [new TextContentBlock { Text = echo.GetRawText() }] };

    internal static string LastLogLineOf(JsonElement artifact)
    {
        if (!artifact.TryGetProperty("log", out var log) ||
            log.ValueKind != JsonValueKind.Array ||
            log.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var last = log[log.GetArrayLength() - 1];
        return last.ValueKind == JsonValueKind.Object &&
            last.TryGetProperty("text", out var text) &&
            text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }

    internal static JsonElement CompactArtifact(JsonElement artifact)
    {
        var compact = new JsonObject
        {
            ["id"] = artifact.GetProperty("id").GetString(),
            ["kind"] = artifact.GetProperty("kind").GetString(),
            ["status"] = artifact.GetProperty("status").GetString(),
            ["version"] = artifact.GetProperty("version").GetInt32(),
            ["order"] = artifact.GetProperty("order").GetInt32(),
            ["frozenAt"] = artifact.GetProperty("frozenAt").GetString(),
            ["updatedAt"] = artifact.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = artifact.GetProperty("body").GetString()?.Length ?? 0,
            ["lastLogLine"] = LastLogLineOf(artifact)
        };

        if (artifact.TryGetProperty("dryRun", out var dryRun) && dryRun.ValueKind == JsonValueKind.True)
        {
            compact["dryRun"] = true;
        }

        if (artifact.TryGetProperty("brief", out var brief) && brief.ValueKind == JsonValueKind.String)
        {
            compact["brief"] = brief.GetString();
        }

        if (artifact.TryGetProperty("statusChanged", out var statusChanged) &&
            statusChanged.ValueKind == JsonValueKind.String)
        {
            compact["statusChanged"] = statusChanged.GetString();
        }

        if (artifact.TryGetProperty("featureStatusChanged", out var featureStatusChanged) &&
            featureStatusChanged.ValueKind == JsonValueKind.String)
        {
            compact["featureStatusChanged"] = featureStatusChanged.GetString();
        }

        if (artifact.TryGetProperty("validationProofVoided", out var validationProofVoided) &&
            validationProofVoided.ValueKind == JsonValueKind.String)
        {
            compact["validationProofVoided"] = validationProofVoided.GetString();
        }

        if (artifact.TryGetProperty("validationProofVoidCause", out var validationProofVoidCause) &&
            validationProofVoidCause.ValueKind == JsonValueKind.String)
        {
            compact["validationProofVoidCause"] = validationProofVoidCause.GetString();
        }

        CarryReplayedFlag(compact, artifact);
        return compact.Deserialize<JsonElement>();
    }

    internal static JsonElement CompactFeature(JsonElement feature)
    {
        var compact = new JsonObject
        {
            ["slug"] = feature.GetProperty("slug").GetString(),
            ["status"] = feature.GetProperty("status").GetString(),
            ["priority"] = feature.GetProperty("priority").GetString(),
            ["version"] = feature.GetProperty("version").GetInt32(),
            ["updatedAt"] = feature.GetProperty("updatedAt").GetString(),
            ["bodyChars"] = feature.GetProperty("body").GetString()?.Length ?? 0
        };
        CarryReplayedFlag(compact, feature);
        return compact.Deserialize<JsonElement>();
    }

    internal static JsonElement KeepReviewOnlyOnTaskArtifacts(JsonElement feature)
    {
        if (feature.ValueKind != JsonValueKind.Object ||
            !feature.TryGetProperty("artifacts", out var artifacts) ||
            artifacts.ValueKind != JsonValueKind.Array)
        {
            return feature;
        }

        var node = JsonNode.Parse(feature.GetRawText())!.AsObject();
        foreach (var artifact in node["artifacts"]!.AsArray())
        {
            if (artifact?["kind"]?.GetValue<string>().Equals("task", StringComparison.OrdinalIgnoreCase) != true)
            {
                artifact!.AsObject().Remove("review");
            }
        }

        return node.Deserialize<JsonElement>();
    }

    internal static JsonElement KeepReviewOnlyOnTasks(JsonElement artifact)
    {
        if (artifact.ValueKind != JsonValueKind.Object ||
            artifact.GetProperty("kind").GetString()?.Equals("task", StringComparison.OrdinalIgnoreCase) == true)
        {
            return artifact;
        }

        var node = JsonNode.Parse(artifact.GetRawText())!.AsObject();
        node.Remove("review");
        return node.Deserialize<JsonElement>();
    }

    internal static string NormalizeKind(string kind)
    {
        var trimmed = kind.Trim();
        if (!ArtifactKinds.Contains(trimmed))
        {
            throw new McpException($"Invalid artifact kind '{kind}'. Expected one of: spec, plan, task, result.");
        }

        return trimmed.ToLowerInvariant();
    }

    internal static string NormalizeReview(string review)
    {
        var trimmed = review.Trim();
        if (!ReviewLevels.Contains(trimmed))
        {
            throw new McpException($"Invalid review level '{review}'. Expected one of: light, hard.");
        }

        return trimmed.ToLowerInvariant();
    }

    internal static string NormalizePriority(string priority)
    {
        var trimmed = priority.Trim();
        if (!FeaturePriorities.Contains(trimmed))
        {
            throw new McpException($"Invalid feature priority '{priority}'. Expected one of: p0, p1, p2, p3.");
        }

        return trimmed.ToLowerInvariant();
    }

    internal static string NormalizeStatus(string status)
    {
        if (!ArtifactStatuses.TryGetValue(status.Trim(), out var normalized))
        {
            throw new McpException(
                $"Invalid artifact status '{status}'. Expected one of: draft, validated, todo, inProgress, review, done, blocked.");
        }

        return normalized;
    }

    internal static string NormalizeFeatureStatus(string status)
    {
        if (!AgentFeatureStatuses.TryGetValue(status.Trim(), out var normalized))
        {
            throw new McpException(
                $"Invalid feature status '{status}' for the agent door. Allowed targets: done (close, from any non-done status) or inProgress (review rollback); every other manual move is human-door only in the Overmind UI.");
        }

        return normalized;
    }
}
