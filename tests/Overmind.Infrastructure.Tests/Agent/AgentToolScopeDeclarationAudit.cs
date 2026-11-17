using System.Text.Json;
using Overmind.Infrastructure.Agent.Tools;

namespace Overmind.Infrastructure.Tests.Agent;

internal static class AgentToolScopeDeclarationAudit
{
    internal static readonly IReadOnlyList<string> SessionScoped =
    [
        "conversation_history", "memory_note", "memory_recall", "memory_remember",
        "poll_replies", "wake_peek", "wave_role_set",
    ];

    internal static readonly IReadOnlyList<string> Discovery =
    [
        "get_board_context", "list_projects", "notify_human",
    ];

    // Marked by task 2: the cross-project filing door. Its authorProjectId obligation is enforced in
    // AgentToolRegistry.ProvenanceRefusalFor, and the rule below keeps any future marking honest about it.
    internal static readonly IReadOnlyList<string> CrossProject =
    [
        "issues_create",
    ];

    // Object-determined doors: the schema can name a stored object without naming the project that
    // owns it. Enforcing this half is feature 246, not 241 — 241 ships argument-level enforcement plus
    // this declaration. The four optional-projectId doors are the dangerous shape: sending the
    // conversation's own projectId satisfies the argument check while the artifactId names any project.
    internal static readonly IReadOnlyList<string> ObjectDeterminedGap =
    [
        "delete_entry", "entry_versions", "issues_append", "issues_get",
        "issues_promote", "issues_set_status", "kit_import", "kit_import_scan",
        "kit_reimport", "planning_append_log", "planning_delete_artifact", "planning_edit_log",
        "planning_patch_artifact", "planning_read_artifact", "planning_read_artifact_diff", "planning_set_status",
        "planning_validate", "read_entry", "update_entry",
    ];

    // A handle carried together with a REQUIRED projectId is a door only if the service resolves the
    // handle globally. ack_entry is not: EntryAckService.AckAsync filters e.Id == entryId &&
    // e.ProjectId == projectId, pinned by EntryAckServiceTests.Ack_EntryOfAnotherProject_ThrowsMissingEntry.
    internal static readonly IReadOnlyList<string> ScopedHandleExceptions =
    [
        "ack_entry",
    ];

    internal static readonly IReadOnlyList<string> ArgumentEnforced =
    [
        "issues_list", "list_entries",
        "planning_ack_directive", "planning_add_feature_dependency", "planning_append_directive", "planning_claim_feature",
        "planning_create_feature", "planning_current_task", "planning_export_backlog", "planning_import_feature",
        "planning_list_artifacts", "planning_list_feature_dependencies", "planning_list_features", "planning_list_lanes",
        "planning_materialize", "planning_materialize_status", "planning_move_feature", "planning_pin_feature_code",
        "planning_read_feature", "planning_remove_feature_dependency", "planning_remove_lane", "planning_set_feature_priority",
        "planning_set_feature_status", "planning_set_lanes", "planning_task_queue", "planning_unclaim_feature",
        "planning_update_feature", "planning_update_lane", "planning_upsert_artifact", "planning_upsert_tasks",
        "read_project", "search", "wave_chat_history", "wave_chat_peek",
        "wave_chat_post", "wave_chat_unread", "wave_rooms", "write_entry",
    ];

    internal static readonly IReadOnlyList<string> NoAddressedTarget =
    [
        "bounce_turn", "kits_list", "record_route",
    ];

    private static readonly IReadOnlyList<string> ObjectHandles =
        ["artifactId", "entryId", "issueId", "kitId"];

    private static readonly IReadOnlyList<string> PathHandles =
        ["path", "targetPath"];

    internal static IReadOnlyList<string> Audit(IReadOnlyList<AgentToolSpec> specs)
    {
        var violations = new List<string>();
        var byName = specs.ToDictionary(spec => spec.Name, StringComparer.Ordinal);

        foreach (var declared in AllDeclared().SelectMany(bucket => bucket.Value.Select(name => (name, bucket.Key))))
        {
            if (!byName.ContainsKey(declared.name))
            {
                violations.Add($"declared in {declared.Key} but no longer registered: {declared.name}");
            }
        }

        foreach (var spec in specs)
        {
            var bucket = BucketOf(spec.Name);
            if (bucket is null)
            {
                violations.Add($"unclassified — {spec.Name} is registered in no declared scope bucket");
            }

            violations.AddRange(PolicyViolations(spec, bucket));
            violations.AddRange(SchemaViolations(spec, bucket));
        }

        return violations;
    }

    internal static Dictionary<string, IReadOnlyList<string>> AllDeclared() => new()
    {
        ["SessionScoped"] = SessionScoped,
        ["Discovery"] = Discovery,
        ["CrossProject"] = CrossProject,
        ["ObjectDeterminedGap"] = ObjectDeterminedGap,
        ["ScopedHandleException"] = ScopedHandleExceptions,
        ["ArgumentEnforced"] = ArgumentEnforced,
        ["NoAddressedTarget"] = NoAddressedTarget,
    };

    internal static string? BucketOf(string name) =>
        AllDeclared().FirstOrDefault(bucket => bucket.Value.Contains(name, StringComparer.Ordinal)).Key;

    internal static bool CarriesProjectArgument(AgentToolSpec spec) => Schema(spec).Properties.Contains("projectId");

    internal static bool ObjectDetermined(AgentToolSpec spec)
    {
        var schema = Schema(spec);
        return schema.Properties.Any(ObjectHandles.Contains) ||
               (!CarriesProjectArgument(spec) && schema.Properties.Any(PathHandles.Contains));
    }

    private static IEnumerable<string> PolicyViolations(AgentToolSpec spec, string? bucket)
    {
        var expected = bucket switch
        {
            "SessionScoped" => AgentToolScope.SessionScoped,
            "Discovery" => AgentToolScope.Discovery,
            "CrossProject" => AgentToolScope.CrossProject,
            _ => AgentToolScope.Enforce,
        };

        if (spec.Scope != expected)
        {
            yield return $"{spec.Name} is declared {bucket} but carries policy {spec.Scope}, expected {expected}";
        }
    }

    private static IEnumerable<string> SchemaViolations(AgentToolSpec spec, string? bucket)
    {
        var schema = Schema(spec);
        var requiredProject = schema.Properties.Contains("projectId") && schema.Required.Contains("projectId");
        var guidHandle = schema.Properties.Any(ObjectHandles.Contains);

        switch (bucket)
        {
            case "SessionScoped" when CarriesProjectArgument(spec):
                yield return $"{spec.Name} is SessionScoped but its schema now addresses a project — " +
                             "reclassify it, a session-scoped tool must take no projectId argument";
                break;
            case "ObjectDeterminedGap" when !ObjectDetermined(spec):
                yield return $"{spec.Name} is declared an object-determined door but its schema no longer addresses an object";
                break;
            case "ScopedHandleException" when !(requiredProject && guidHandle):
                yield return $"{spec.Name} is declared a scoped-handle exception but no longer carries a required projectId with a handle";
                break;
            case "CrossProject" when !schema.Properties.Contains("authorProjectId"):
                yield return $"{spec.Name} is marked CrossProject but takes no authorProjectId — " +
                             "the provenance check would have nothing to verify and would refuse every call";
                break;
            case "ArgumentEnforced" when ObjectDetermined(spec):
                yield return $"{spec.Name} addresses an object by handle and belongs in ObjectDeterminedGap or ScopedHandleException";
                break;
            case "NoAddressedTarget" when CarriesProjectArgument(spec) || ObjectDetermined(spec):
                yield return $"{spec.Name} is declared to address no target but its schema now carries projectId or a handle";
                break;
            default:
                if (requiredProject && guidHandle && !ScopedHandleExceptions.Contains(spec.Name, StringComparer.Ordinal))
                {
                    yield return $"{spec.Name} resolves a {string.Join("/", schema.Properties.Intersect(ObjectHandles))} " +
                                 "inside a required projectId — prove the service scopes the handle, then declare it in ScopedHandleExceptions";
                }

                if (!ObjectDetermined(spec) && !CarriesProjectArgument(spec) &&
                    bucket is not ("SessionScoped" or "Discovery" or "NoAddressedTarget"))
                {
                    yield return $"{spec.Name} addresses neither a project argument nor an object — " +
                                 $"it must be declared in a scope bucket that matches its target (found {bucket})";
                }

                break;
        }
    }

    private static SchemaShape Schema(AgentToolSpec spec)
    {
        using var document = JsonDocument.Parse(spec.ParametersJson);
        var root = document.RootElement;
        var properties = root.TryGetProperty("properties", out var props)
            ? new HashSet<string>(props.EnumerateObject().Select(property => property.Name), StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var required = root.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array
            ? new HashSet<string>(req.EnumerateArray().Select(item => item.GetString() ?? string.Empty), StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        return new SchemaShape(properties, required);
    }

    private readonly record struct SchemaShape(HashSet<string> Properties, HashSet<string> Required);
}
