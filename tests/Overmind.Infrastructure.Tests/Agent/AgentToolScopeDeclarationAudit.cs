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
    // owns it. The dangerous shape was the four optional-projectId doors — sending the conversation's
    // own projectId satisfied 241's argument check while the artifactId still named any project.
    // Closed by feature 246 (T1 artifacts, T2 issues, T3 entries): the host compares the object's owning
    // project with the conversation's at the point the row is already loaded. Kept as its own bucket
    // rather than folded into ArgumentEnforced because the check answers a different question - whose
    // object is this, not which project did the argument name - and the derivation below stays blind to
    // whichever policy a door declares.
    internal static readonly IReadOnlyList<string> DeclaredEnforced =
    [
        "delete_entry", "entry_versions", "issues_append", "issues_get",
        "issues_promote", "issues_set_status", "planning_append_log", "planning_delete_artifact",
        "planning_edit_log", "planning_patch_artifact", "planning_read_artifact", "planning_read_artifact_diff",
        "planning_set_status", "planning_validate", "read_entry", "update_entry",
    ];

    // Still open, and open on purpose: a kit is a global library object — SkillKits carries no ProjectId —
    // so there is no owning project to compare against and "project scope" has no meaning for these three.
    // Tracked as a declared hole in board card ba0d50f4, not as enforcement the server forgot to add.
    internal static readonly IReadOnlyList<string> ObjectDeterminedGap =
    [
        "kit_import", "kit_import_scan", "kit_reimport",
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

        foreach (var clash in AllDeclared().Values.SelectMany(bucket => bucket)
                     .GroupBy(name => name, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            violations.Add(
                $"declared in {clash.Count()} buckets — {clash.Key}: buckets must be disjoint or the coverage " +
                "count silently double-counts a door and can equal the registry total while hiding another");
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
            violations.AddRange(DispatchViolations(spec));
        }

        return violations;
    }

    internal static Dictionary<string, IReadOnlyList<string>> AllDeclared() => new()
    {
        ["SessionScoped"] = SessionScoped,
        ["Discovery"] = Discovery,
        ["CrossProject"] = CrossProject,
        ["DeclaredEnforced"] = DeclaredEnforced,
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

    // The dispatch-shape rules of feature 246 T0: a spec that declares a caller-scoped handler must reach the
    // request through it on every branch the loop can take, otherwise the privileged branch is the unchecked one.
    private static IEnumerable<string> DispatchViolations(AgentToolSpec spec)
    {
        if (spec.ScopedInvoke is not null && spec.DelegatedInvoke is not null && spec.DelegatedScopedInvoke is null)
        {
            yield return $"{spec.Name} declares a caller-scoped handler and an unscoped delegated handler: " +
                         "arming delegated approvals would dispatch the object-addressed call without the " +
                         "conversation scope — declare DelegatedScopedInvoke for it";
        }

        if (spec.DelegatedScopedInvoke is not null && spec.ScopedInvoke is null)
        {
            yield return $"{spec.Name} declares a caller-scoped delegated handler but no caller-scoped plain " +
                         "handler: the same door would reach the host unscoped whenever delegation is off";
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
            case "DeclaredEnforced" when !ObjectDetermined(spec):
                yield return $"{spec.Name} is declared caller-enforced but its schema no longer addresses an " +
                             "object — reclassify it, the bucket it sits in is a claim about a comparison";
                break;
            case "DeclaredEnforced" when spec.ScopedInvoke is null:
                yield return $"{spec.Name} is declared caller-enforced but carries no caller-scoped handler: the " +
                             "audit would claim a comparison the transport never sends, which is worse than an " +
                             "open gap — declare it in ObjectDeterminedGap until it is converted";
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
