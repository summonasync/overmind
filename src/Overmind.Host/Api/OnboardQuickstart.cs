namespace Overmind.Host.Api;

public static class OnboardQuickstart
{
    public const string Text = """
        Overmind onboarding quickstart — run the MCP tools in this order:

        Materialization: the BACKLOG/ tree under the project root is rewritten after every planning mutation when the resolved strategy is on; the materialization block of this response carries the resolved strategy, targetDir, lastSuccess (null = never materialized) and lastAttempt (null = never attempted). When the validation block's humanGate is on, planning_validate returns STOP until the user approves the artifact in the Overmind UI.

        1. overmind_onboard — resolves your project from the reported cwd and returns the project id, board summary, and this quickstart.
        2. planning_list_features — the full board view; pick the feature to work on from planning_task_queue: the board order IS the queue, first on the board = next in the queue. Prefer the pinned current feature when one is set.
        3. planning_current_task — returns the single next task, or a stop/empty reason. Obey STOP lines; ask the user instead of working around them.
        4. Work the loop — planning_upsert_artifact for specs, plans, and tasks; planning_set_status with a log note after each step.
        5. When all tasks are done, write the result artifact.

        Escape hatch: set the OVERMIND_PROJECT_ID environment variable to skip cwd matching and pin a project id.

        Feature lifecycle: draft → ready → inProgress → review → done.
        Auto-transitions: artifact create → inProgress; all tasks done + spec + plan → review; task create on review → back to inProgress.
        planning_set_feature_status reaches done from ANY non-done status; done is terminal.
        A done feature receiving a new artifact stays done — no transition fires.
        Without a plan there is no auto-promotion to review, even when all tasks are done — spec AND plan are required.
        Import restores statuses verbatim from the materialized files — an imported done feature was not auto-closed.
        planning_import_feature imports one materialized feature folder from disk in a single call — give its absolute path under the project root.
        Log lines carry [agent]/[ui]/[auto]/[import] markers saying who moved a status.
        Artifact statuses — spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked. Validated is the documents' terminal; result has no done.
        Before working, read notes/decisions/guards via overmind://resources and list_entries filters.
        """;
}
