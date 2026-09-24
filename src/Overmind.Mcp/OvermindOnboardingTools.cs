using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Overmind.Mcp;

[McpServerToolType]
public sealed class OvermindOnboardingTools
{
    private const string ProjectIdEnvironmentVariable = "OVERMIND_PROJECT_ID";

    private readonly OvermindApiClient _api;

    public OvermindOnboardingTools(OvermindApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "overmind_onboard", Destructive = false, Idempotent = false)]
    [Description("Resolves the project from the adapter's working directory (creating it if needed) and returns the project id, board summary and onboarding quickstart. The natural first call for an agent that does not know its projectId yet; agents working with an explicit projectId may skip it. The payload also carries a materialization block (strategy, targetDir, trigger, lastRun, and the skipped-file count with the head of skipped paths and reasons when the last pass skipped any) and a validation block (humanGate). Set OVERMIND_PROJECT_ID to a valid project guid to skip cwd matching — use this for symlinked or worktree layouts where the resolved cwd differs from the registered project path.")]
    public async Task<string> Onboard(CancellationToken ct)
    {
        var payload = await Guarded(() => _api.PostAsync("/api/onboard", BuildRequest(), ct));
        return Format(payload);
    }

    private static object BuildRequest()
    {
        var overrideId = Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable);
        return Guid.TryParse(overrideId, out var projectId)
            ? new { projectId }
            : new { cwd = Environment.CurrentDirectory };
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

    private static string Format(JsonElement payload)
    {
        var projectName = payload.GetProperty("projectName").GetString() ?? "unknown";
        var projectId = payload.GetProperty("projectId").GetString() ?? string.Empty;

        var output = new StringBuilder();
        output.AppendLine($"Project: {projectName} ({projectId})");
        if (payload.TryGetProperty("created", out var created) &&
            created.ValueKind == JsonValueKind.True &&
            created.GetBoolean())
        {
            output.AppendLine($"Created project '{projectName}'.");
        }

        var board = payload.GetProperty("board");
        if (board.TryGetProperty("countsByStatus", out var counts) &&
            counts.ValueKind == JsonValueKind.Object &&
            counts.EnumerateObject().Any())
        {
            output.AppendLine($"Board: {string.Join(", ", counts.EnumerateObject().Select(p => $"{p.Name}: {p.Value}"))}");
        }
        else
        {
            output.AppendLine("Board: no features yet.");
        }

        if (board.TryGetProperty("currentSlug", out var current) &&
            current.ValueKind == JsonValueKind.String)
        {
            output.AppendLine($"Current feature: {current.GetString()}");
        }

        AppendMaterialization(payload, output);
        AppendValidation(payload, output);

        output.AppendLine();
        output.Append(payload.GetProperty("quickstart").GetString());
        return output.ToString();
    }

    private static void AppendMaterialization(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("materialization", out var block) ||
            block.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!block.TryGetProperty("strategy", out var strategy) ||
            strategy.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var targetDir = block.TryGetProperty("targetDir", out var dir) ? dir.GetString() : null;
        var lastRun = block.TryGetProperty("lastRun", out var run) &&
                      run.ValueKind == JsonValueKind.String
            ? run.GetString()
            : "never";

        var failureSuffix = string.Empty;
        if (block.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String &&
            error.GetString() is { Length: > 0 } errorText)
        {
            var failedAt = block.TryGetProperty("failedAt", out var at) &&
                           at.ValueKind == JsonValueKind.String
                ? at.GetString()
                : null;
            failureSuffix = failedAt is { Length: > 0 }
                ? $" (failed {failedAt}: {errorText})"
                : $" (failed: {errorText})";
        }

        output.AppendLine(
            $"Materialization: strategy={strategy.GetString()}, targetDir={targetDir ?? "?"}, lastRun={lastRun}{failureSuffix}");

        AppendSkipped(block, output);

        if (block.TryGetProperty("trigger", out var trigger) &&
            trigger.ValueKind == JsonValueKind.String &&
            trigger.GetString() is { Length: > 0 } triggerText)
        {
            output.AppendLine(triggerText);
        }
    }

    private static void AppendSkipped(JsonElement block, StringBuilder output)
    {
        if (!block.TryGetProperty("skippedCount", out var count) ||
            count.ValueKind != JsonValueKind.Number)
        {
            return;
        }

        output.AppendLine($"Skipped on the last pass: {count.GetInt32()} file(s)");
        if (!block.TryGetProperty("skipped", out var head) ||
            head.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entry in head.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                output.AppendLine($"- {entry.GetString()}");
            }
        }
    }

    private static void AppendValidation(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("validation", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("humanGate", out var gate))
        {
            return;
        }

        var state = gate.ValueKind switch
        {
            JsonValueKind.True => "on",
            JsonValueKind.False => "off",
            _ => null
        };

        if (state is not null)
        {
            output.AppendLine($"Validation: humanGate={state}");
        }
    }
}
