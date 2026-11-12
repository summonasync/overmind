namespace Overmind.Host;

internal static class PlanningCliCommands
{
    public const string Usage =
        """
        usage: overmind planning <command> [args] [--project <guid>] [--json]

        read:
          projects list
          features list --project <guid>
          feature read <slug> --project <guid>
          artifact read <guid|alias> [--sections a,b] --project <guid>
          queue [--limit n] --project <guid>
          current <slug> --project <guid>
          lanes --project <guid>
          artifact diff <guid|alias> [--from n] [--to n] --project <guid>
          materialization-status --project <guid>
          watch [--alias a] [--mentions | --replies] [--timeout 30m] [--interval 10s]

        draft mutations (agent session required):
          claim <slug> [--alias a] [--vendor v] [--override-lane-check] --project <guid>
          unclaim <slug> --project <guid>
          artifact upsert <slug> --kind spec|plan|task|result --body-file <path> [--body <text>]
              [--order n] [--review light|hard] [--brief text] [--log-note text]
              [--blocked-by 1,2] --project <guid>
          tasks batch <slug> --file <tasks.json> --project <guid>
          artifact patch <guid|alias> (--section name (--content-file <path> | --content <text>)
              | --ac-index n | --ac-text text [--untick] | --brief text) [--log-note text] --project <guid>
          log append <guid|alias> --note text --project <guid>
          log edit <guid|alias> --text text --project <guid>
          task status <guid|alias> --status todo|inProgress|review|done|blocked --log-note text --project <guid>
          feature update <slug> [--title text] [--body-file path] [--priority p0..p3]
              [--lane name] [--code-surfaces glob,glob] --log-note text --project <guid>
          directive append <slug> --text text --project <guid>
          directive ack <slug> <directiveId> --project <guid>

        human gates are not available here: validate, feature status done and
        set_feature_priority stay on the Overmind UI (or the MCP approval flow).
        """;

    private static readonly HashSet<string> Reads = new(StringComparer.Ordinal)
    {
        "projects list",
        "features list",
        "feature read",
        "artifact read",
        "queue",
        "current",
        "lanes",
        "artifact diff",
        "materialization-status",
        "watch",
    };

    private static readonly HashSet<string> DraftMutations = new(StringComparer.Ordinal)
    {
        "claim",
        "unclaim",
        "artifact upsert",
        "artifact patch",
        "tasks batch",
        "log append",
        "log edit",
        "task status",
        "feature update",
        "directive append",
        "directive ack",
    };

    public static bool IsKnown(string command) =>
        !string.IsNullOrEmpty(command) && (Reads.Contains(command) || DraftMutations.Contains(command));

    public static bool IsDraftMutation(string command) => DraftMutations.Contains(command);
}
