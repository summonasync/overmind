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
    [Description("Resolves the project from the adapter's working directory (creating it if needed) and " +
        "returns project id, board summary, guardrail blocks (materialization, validation gate, agent " +
        "identity, semantic search, adapter version) and the quickstart. The natural first call without a " +
        "projectId; brief=true answers id, counts, humanGate and version only.")]
    public async Task<string> Onboard(
        [Description("True for the cheap repeat answer: project id, board counts, humanGate, adapter version.")] bool brief = false,
        CancellationToken ct = default)
    {
        var payload = await Guarded(() => _api.PostAsync("/api/onboard", BuildRequest(), ct));
        return brief ? FormatBrief(payload) : Format(payload);
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

    private static string FormatBrief(JsonElement payload)
    {
        var projectName = payload.GetProperty("projectName").GetString() ?? "unknown";
        var projectId = payload.GetProperty("projectId").GetString() ?? string.Empty;

        var output = new StringBuilder();
        output.AppendLine($"Project: {projectName} ({projectId})");

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

        if (HumanGateState(payload) is { } gate)
        {
            output.AppendLine($"Validation: humanGate={gate}");
        }

        AppendBriefAdapter(payload, output);
        return output.ToString();
    }

    private static void AppendBriefAdapter(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("adapter", out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var version = block.TryGetProperty("adapterVersion", out var value) &&
                      value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

        var suffix = block.TryGetProperty("warning", out var warning) &&
                     warning.ValueKind == JsonValueKind.String &&
                     warning.GetString() is { Length: > 0 } warningText
            ? $" — {warningText}"
            : string.Empty;

        output.AppendLine($"Adapter: {version ?? "unknown"}{suffix}");
    }

    private static string? HumanGateState(JsonElement payload)
    {
        if (!payload.TryGetProperty("validation", out var block) ||
            !block.TryGetProperty("humanGate", out var gate))
        {
            return null;
        }

        return gate.ValueKind switch
        {
            JsonValueKind.True => "on",
            JsonValueKind.False => "off",
            _ => null
        };
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
        AppendIdentity(payload, output);
        AppendChurn(payload, output);
        AppendSemantic(payload, output);
        AppendAdapter(payload, output);
        AppendRescue(payload, output);

        output.AppendLine();
        output.Append(payload.GetProperty("quickstart").GetString());
        return output.ToString();
    }

    private static void AppendAdapter(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("adapter", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("warning", out var warning) ||
            warning.ValueKind != JsonValueKind.String ||
            warning.GetString() is not { Length: > 0 } warningText)
        {
            return;
        }

        var adapterVersion = block.TryGetProperty("adapterVersion", out var version) &&
                             version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;

        var hostVersion = block.TryGetProperty("hostVersion", out var host) &&
                          host.ValueKind == JsonValueKind.String
            ? host.GetString()
            : null;

        output.AppendLine($"Adapter: {adapterVersion ?? "unknown"} vs host {hostVersion ?? "unknown"}: {warningText}");
    }

    private static void AppendRescue(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("rescue", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("policyText", out var policy) ||
            policy.ValueKind != JsonValueKind.String ||
            policy.GetString() is not { Length: > 0 } policyText)
        {
            return;
        }

        output.AppendLine($"Rescue policy: {policyText}");
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
        var lastSuccess = block.TryGetProperty("lastSuccess", out var success) &&
                          success.ValueKind == JsonValueKind.String
            ? success.GetString()
            : "never";
        var lastAttempt = block.TryGetProperty("lastAttempt", out var attempt) &&
                          attempt.ValueKind == JsonValueKind.String
            ? attempt.GetString()
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
            $"Materialization: strategy={strategy.GetString()}, targetDir={targetDir ?? "?"}, " +
            $"lastSuccess={lastSuccess}, lastAttempt={lastAttempt}{failureSuffix}");

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

        output.AppendLine($"Skipped at the last successful pass (byte-identical no-ops): {count.GetInt32()} file(s)");
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
        if (HumanGateState(payload) is not { } state)
        {
            return;
        }

        var guidance = payload.GetProperty("validation").TryGetProperty("guidance", out var text) &&
                       text.ValueKind == JsonValueKind.String &&
                       text.GetString() is { Length: > 0 } guidanceText
            ? $" — {guidanceText}"
            : string.Empty;

        output.AppendLine($"Validation: humanGate={state}{guidance}");
    }

    private static void AppendIdentity(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("agentIdentity", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("namingRules", out var rules) ||
            rules.ValueKind != JsonValueKind.String ||
            rules.GetString() is not { Length: > 0 } rulesText)
        {
            return;
        }

        var (alias, vendor, source) = EffectiveIdentity(block);

        output.AppendLine(
            $"Agent identity: alias '{alias}', vendor '{vendor}' (source: {source}) — " +
            "pass this pair to planning_claim_feature when claiming a feature; the project's naming rules:");
        output.AppendLine(rulesText);

        if (source == "session")
        {
            output.AppendLine(
                "This pair is already in force on your session row, not on the adapter's environment: claim under " +
                "it as shown. session_identity_set only renames the session — held claims re-attribute with the rename.");
        }

        if (!AgentIdentity.IsPlaceholderAlias(alias))
        {
            return;
        }

        output.AppendLine(
            $"Next step: '{alias}' is the shared placeholder — invent your own alias per the naming rules above " +
            "(or restore your stable one) and set it via session_identity_set BEFORE claiming; heading to a lane, " +
            "planning_list_lanes first and adopt/bind the lane's boundAlias via session_identity_set before the claim " +
            "(flow: onboard → identity → lane → claim → work).");
    }

    // The session row outranks the adapter's own environment: a restart restore writes the pair there,
    // and a runtime rename fills the in-memory slot. The source word names whichever of the three
    // actually carries the pair the board will read.
    private static (string Alias, string Vendor, string Source) EffectiveIdentity(JsonElement block)
    {
        var slot = AdapterIdentityState.TryGet();
        var envAlias = AgentIdentity.ResolveAlias();
        var envVendor = AgentIdentity.ResolveVendor();

        if (ReadPair(block) is { } session && !AgentIdentity.IsPlaceholderAlias(session.Alias))
        {
            if (slot is not null && Matches(session, slot.Alias, slot.Vendor))
            {
                return (session.Alias, session.Vendor, "slot");
            }

            if (!AgentIdentity.IsPlaceholderAlias(envAlias) && Matches(session, envAlias, envVendor))
            {
                return (session.Alias, session.Vendor, "env");
            }

            return (session.Alias, session.Vendor, "session");
        }

        if (slot is not null)
        {
            return AgentIdentity.IsPlaceholderAlias(slot.Alias)
                ? (slot.Alias, slot.Vendor, "placeholder")
                : (slot.Alias, slot.Vendor, "slot");
        }

        return AgentIdentity.IsPlaceholderAlias(envAlias)
            ? (envAlias, envVendor, "placeholder")
            : (envAlias, envVendor, "env");
    }

    private static (string Alias, string Vendor)? ReadPair(JsonElement block)
    {
        if (block.TryGetProperty("alias", out var alias) &&
            alias.ValueKind == JsonValueKind.String &&
            alias.GetString() is { Length: > 0 } aliasText)
        {
            var vendor = block.TryGetProperty("vendor", out var vendorValue) &&
                         vendorValue.ValueKind == JsonValueKind.String
                ? vendorValue.GetString()
                : null;

            return (aliasText, vendor ?? AgentIdentity.ResolveVendor());
        }

        return null;
    }

    private static bool Matches((string Alias, string Vendor) pair, string alias, string vendor) =>
        string.Equals(pair.Alias, alias, StringComparison.Ordinal) &&
        string.Equals(pair.Vendor, vendor, StringComparison.Ordinal);

    private static void AppendChurn(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("claimChurn", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("sessions", out var sessions) ||
            sessions.ValueKind != JsonValueKind.Array ||
            sessions.GetArrayLength() == 0)
        {
            return;
        }

        output.AppendLine("Claim churn: holders that stopped answering while still holding claims:");
        var shown = 0;
        foreach (var row in sessions.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            shown++;
            output.AppendLine(
                $"- '{Word(row, "alias")}' by '{Word(row, "vendor")}' " +
                $"({Word(row, "liveness")}, {Silence(row)}): holds {SlugList(row)}");
        }

        var dropped = Math.Max(
            block.TryGetProperty("sessionsTotal", out var total) &&
            total.ValueKind == JsonValueKind.Number
                ? total.GetInt32()
                : sessions.GetArrayLength(),
            shown) - shown;

        if (dropped > 0)
        {
            output.AppendLine(
                $"(+{dropped} more holders not listed, {shown + dropped} churned in total)");
        }

        output.AppendLine(
            "If that pair is yours, your next write adopts these claims; if it is not, " +
            "planning_claim_feature is the only take-over door.");
    }

    private static string Silence(JsonElement row)
    {
        if (!row.TryGetProperty("deathAgeMinutes", out var age) ||
            age.ValueKind != JsonValueKind.Number)
        {
            return "age unknown";
        }

        var minutes = age.GetInt32();
        if (minutes < 60)
        {
            return $"silent {minutes}m";
        }

        if (minutes < 24 * 60)
        {
            return $"silent {minutes / 60}h {minutes % 60:00}m";
        }

        return $"silent {minutes / (60 * 24)}d {(minutes % (60 * 24)) / 60}h";
    }

    private static string SlugList(JsonElement row)
    {
        if (!row.TryGetProperty("slugs", out var slugs) || slugs.ValueKind != JsonValueKind.Array)
        {
            return "(none)";
        }

        List<string> listed = [.. slugs.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)];

        if (listed.Count == 0)
        {
            return "(none)";
        }

        var total = row.TryGetProperty("slugsTotal", out var count) &&
                    count.ValueKind == JsonValueKind.Number
            ? count.GetInt32()
            : listed.Count;

        return string.Join(", ", listed) +
               (total > listed.Count ? $" +{total - listed.Count} more" : string.Empty);
    }

    private static string Word(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "?"
            : "?";

    private static void AppendSemantic(JsonElement payload, StringBuilder output)
    {
        if (!payload.TryGetProperty("semantic", out var block) ||
            block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("available", out var available))
        {
            return;
        }

        if (available.ValueKind == JsonValueKind.True)
        {
            output.AppendLine("Semantic search: available");
            return;
        }

        if (available.ValueKind == JsonValueKind.False &&
            block.TryGetProperty("reason", out var reason) &&
            reason.ValueKind == JsonValueKind.String &&
            reason.GetString() is { Length: > 0 } reasonText)
        {
            output.AppendLine($"Semantic search: unavailable ({reasonText})");
        }
    }
}
