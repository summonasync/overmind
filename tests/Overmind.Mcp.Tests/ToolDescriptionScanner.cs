using System.Reflection;
using System.Text;
using ModelContextProtocol.Server;

namespace Overmind.Mcp.Tests;

internal sealed record ParameterMeasurement(string Name, int Chars);

internal sealed record ToolMeasurement(
    string ToolName,
    string DeclaringType,
    int DescriptionChars,
    IReadOnlyList<ParameterMeasurement> Parameters)
{
    public int ParameterChars => Parameters.Sum(p => p.Chars);

    public int TotalChars => DescriptionChars + ParameterChars;

    public bool BreaksToolDescription => DescriptionChars > DescriptionBudgets.ToolDescriptionMax;

    public bool BreaksParameter => Parameters.Any(p => p.Chars > DescriptionBudgets.ParameterDescriptionMax);

    public bool BreaksPerToolTotal => TotalChars > DescriptionBudgets.PerToolTotalMax;

    public string Breaks
    {
        get
        {
            var parts = new List<string>(3);
            if (BreaksToolDescription) parts.Add("tool");
            if (BreaksParameter) parts.Add("param");
            if (BreaksPerToolTotal) parts.Add("per-tool");
            return parts.Count == 0 ? "-" : string.Join(",", parts);
        }
    }
}

internal static class ToolDescriptionScanner
{
    public const int ExpectedToolTypeCount = 20;

    private const BindingFlags ToolMethodFlags =
        BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // The planning pin: the frozen planning surface. Journal-only tools must never enter this list —
    // the planning profile is asserted byte-identical against it.
    public static readonly string[] ExpectedToolNames =
    [
        "ack_entry", "append_entry", "delete_entry", "issues_append", "issues_create", "issues_get",
        "issues_list", "issues_promote", "issues_set_status", "lift_entry", "list_entries", "list_projects",
        "overmind_briefing", "overmind_notify", "overmind_onboard", "overmind_replies", "overmind_wake_peek",
        "overmind_wave_snapshot", "planning_ack_directive", "planning_add_feature_dependency",
        "planning_append_directive", "planning_append_log", "planning_batch", "planning_claim_feature",
        "planning_create_feature", "planning_create_lane", "planning_current_task", "planning_delete_artifact",
        "planning_edit_log", "planning_export_backlog", "planning_feature_code", "planning_import_feature",
        "planning_list_artifacts", "planning_list_feature_dependencies", "planning_list_features",
        "planning_list_lanes", "planning_materialize", "planning_materialize_status", "planning_move_feature",
        "planning_patch_artifact", "planning_patch_feature", "planning_pin_feature_code",
        "planning_read_artifact", "planning_read_artifact_diff", "planning_read_feature",
        "planning_remove_feature_dependency", "planning_remove_lane", "planning_rescue_scan",
        "planning_set_feature_priority",
        "planning_set_feature_status", "planning_set_lanes", "planning_set_status", "planning_task_queue",
        "planning_unclaim_feature", "planning_update_feature", "planning_upsert_artifact",
        "planning_upsert_tasks", "planning_validate", "read_entry", "read_project_manifest", "search",
        "session_identity_set", "update_entry", "wave_chat_peek", "wave_chat_post", "wave_chat_unread",
        "wave_role_set", "write_entry"
    ];

    // The journal pin: the journal profile's own family (T2 adds journal_resolve_links here).
    public static readonly string[] ExpectedJournalToolNames =
    [
        "journal_append", "journal_capture", "journal_list", "journal_read", "journal_search",
    ];

    public static string[] ExpectedUnionToolNames => [.. ExpectedToolNames, .. ExpectedJournalToolNames];

    public static Snapshot Scan() => Scan(typeof(WaveRoleTools).Assembly);

    public static IReadOnlyList<Type> ToolTypes(Assembly adapterAssembly) =>
        adapterAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<MethodInfo> ToolMethods(Assembly adapterAssembly)
    {
        var methods = new List<MethodInfo>();
        foreach (var type in ToolTypes(adapterAssembly))
        {
            methods.AddRange(type.GetMethods(ToolMethodFlags)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null));
        }

        return methods;
    }

    public static int Chars(string? description) =>
        description is null ? 0 : description.EnumerateRunes().Count();

    public static Snapshot Scan(Assembly adapterAssembly)
    {
        var toolTypes = ToolTypes(adapterAssembly);
        var tools = new List<ToolMeasurement>();
        foreach (var type in toolTypes)
        {
            foreach (var method in type.GetMethods(ToolMethodFlags))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is not { } toolAttribute)
                {
                    continue;
                }

                var parameters = method.GetParameters()
                    .Select(p => new ParameterMeasurement(
                        p.Name ?? "_",
                        Chars(p.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description)))
                    .ToList();

                tools.Add(new ToolMeasurement(
                    string.IsNullOrWhiteSpace(toolAttribute.Name) ? method.Name : toolAttribute.Name!,
                    type.Name,
                    Chars(method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description),
                    parameters));
            }
        }

        return new Snapshot(
            tools.OrderBy(t => t.ToolName, StringComparer.Ordinal).ToList(),
            toolTypes.Select(t => t.Name).ToList());
    }

    internal sealed record Snapshot(IReadOnlyList<ToolMeasurement> Tools, IReadOnlyList<string> ToolTypes)
    {
        public int DescriptionChars => Tools.Sum(t => t.DescriptionChars);

        public int ParameterChars => Tools.Sum(t => t.ParameterChars);

        public int TotalChars => DescriptionChars + ParameterChars;
    }
}

internal static class DescriptionDietReport
{
    private const int ParameterDetailLimit = 60;

    public static IReadOnlyList<string> ViolatedBudgets(ToolDescriptionScanner.Snapshot snapshot)
    {
        var violated = new List<string>();

        var toolCount = Count(snapshot, t => t.BreaksToolDescription);
        if (toolCount > 0)
        {
            violated.Add($"tool description > {DescriptionBudgets.ToolDescriptionMax}: {toolCount} tools");
        }

        var parameterCount = Count(snapshot, t => t.BreaksParameter);
        if (parameterCount > 0)
        {
            violated.Add($"parameter description > {DescriptionBudgets.ParameterDescriptionMax}: {parameterCount} tools carry one");
        }

        var perToolCount = Count(snapshot, t => t.BreaksPerToolTotal);
        if (perToolCount > 0)
        {
            violated.Add($"per-tool total > {DescriptionBudgets.PerToolTotalMax}: {perToolCount} tools");
        }

        if (snapshot.TotalChars > DescriptionBudgets.RepoTotalMax)
        {
            violated.Add($"repo total {snapshot.TotalChars} > {DescriptionBudgets.RepoTotalMax} " +
                $"by {snapshot.TotalChars - DescriptionBudgets.RepoTotalMax} chars");
        }

        return violated;
    }

    public static string Violations(ToolDescriptionScanner.Snapshot snapshot)
    {
        var offenders = snapshot.Tools
            .Where(t => t.BreaksToolDescription || t.BreaksParameter || t.BreaksPerToolTotal)
            .OrderByDescending(t => t.TotalChars)
            .ToList();

        var fatParameters = snapshot.Tools
            .SelectMany(t => t.Parameters
                .Where(p => p.Chars > DescriptionBudgets.ParameterDescriptionMax)
                .Select(p => $"{t.ToolName}.{p.Name} = {p.Chars}"))
            .ToList();

        var violated = ViolatedBudgets(snapshot);

        var lines = new List<string>
        {
            $"scanned {snapshot.Tools.Count} tools in {snapshot.ToolTypes.Count} [McpServerToolType] types: " +
            $"{snapshot.DescriptionChars} tool-description chars + {snapshot.ParameterChars} parameter chars " +
            $"= {snapshot.TotalChars} total",
            $"budgets: tool <= {DescriptionBudgets.ToolDescriptionMax}, param <= {DescriptionBudgets.ParameterDescriptionMax}, " +
            $"per-tool <= {DescriptionBudgets.PerToolTotalMax}, repo <= {DescriptionBudgets.RepoTotalMax}",
            violated.Count == 0 ? "violated budgets: none" : "violated budgets: " + string.Join(" | ", violated),
            ""
        };

        if (offenders.Count == 0)
        {
            lines.Add("every tool fits its per-tool budgets");
        }
        else
        {
            lines.Add("tool".PadRight(34) + "desc".PadLeft(7) + "param".PadLeft(7) + "total".PadLeft(7) + "  breaks");
            foreach (var tool in offenders)
            {
                lines.Add(tool.ToolName.PadRight(34)
                    + tool.DescriptionChars.ToString().PadLeft(7)
                    + tool.ParameterChars.ToString().PadLeft(7)
                    + tool.TotalChars.ToString().PadLeft(7)
                    + "  " + tool.Breaks);
            }
        }

        lines.Add("");
        lines.Add(fatParameters.Count == 0
            ? "no parameter description exceeds the budget"
            : $"parameters over {DescriptionBudgets.ParameterDescriptionMax} chars ({fatParameters.Count} total):");
        foreach (var parameter in fatParameters.Take(ParameterDetailLimit))
        {
            lines.Add("  " + parameter);
        }

        if (fatParameters.Count > ParameterDetailLimit)
        {
            lines.Add($"  ... {fatParameters.Count - ParameterDetailLimit} more");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int Count(ToolDescriptionScanner.Snapshot snapshot, Func<ToolMeasurement, bool> predicate) =>
        snapshot.Tools.Count(predicate);
}
