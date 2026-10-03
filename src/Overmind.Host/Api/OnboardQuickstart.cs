namespace Overmind.Host.Api;

public static class OnboardQuickstart
{
    public const string Text = """
        Overmind onboarding quickstart — run the MCP tools in this order:

        Materialization: the BACKLOG/ tree under the project root is rewritten after every planning mutation when the resolved strategy is on; the materialization block of this response carries the resolved strategy, targetDir, lastSuccess (null = never materialized) and lastAttempt (null = never attempted). When the validation block's humanGate is on, planning_validate returns STOP until the user approves the artifact in the Overmind UI.

        1. overmind_onboard — resolves your project from the reported cwd and returns the project id, board summary, and this quickstart.
        2. planning_list_features — the full board view; pick the feature to work on from planning_task_queue: the board order IS the queue, first on the board = next in the queue. Claim the pick with planning_claim_feature (your alias+vendor bind the claim to your adapter session — the agentIdentity block of this response carries the project's naming rules; follow them).
        3. planning_current_task — returns the single next task, or a stop/empty reason. Obey STOP lines; ask the user instead of working around them.
        4. Work the loop — planning_upsert_artifact for specs, plans, and tasks; planning_set_status with a log note after each step.
        5. When all tasks are done, write the result artifact.

        Escape hatch: set the OVERMIND_PROJECT_ID environment variable to skip cwd matching and address a specific project id directly.

        Feature lifecycle: draft → ready → inProgress → review → done.
        Review invariant: review ⟺ spec+plan validated AND all tasks done.
        Auto-transitions: artifact create → inProgress; all tasks done + spec + plan validated → review; task create on review → back to inProgress.
        Editing a validated spec or plan demotes it to draft and pulls a review feature back to inProgress in the same write ([auto] line records it); done features are never resurrected.
        planning_set_feature_status reaches done from ANY non-done status; done is terminal.
        A done feature receiving a new artifact stays done — no transition fires.
        Without a plan there is no auto-promotion to review, even when all tasks are done — spec AND plan are required.
        Import restores statuses verbatim from the materialized files — an imported done feature was not auto-closed.
        planning_import_feature imports one materialized feature folder from disk in a single call — give its absolute path under the project root.
        Log lines carry [agent]/[ui]/[auto]/[import] markers saying who moved a status.
        Artifact statuses — spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked. Validated is the documents' terminal; result has no done.
        Before working, read notes/decisions/guards with list_entries (type=note|decision|gate|guard) and read_entry.
        Claims: a claimed feature is locked to the holder session — other sessions get 409 naming the holder on feature mutations; claims release themselves when the holder session ends or goes stale past the project threshold, stale/ended claims are take-over-able, and planning_unclaim_feature hands the feature back. Unclaim when you hand off.
        Foreign projects: need a fix in code you do not own? Do not touch their repo — file an issue on their board: list_projects → issues_create(projectId=theirs, authorProjectId=yours). Follow-ups = issues_append; the owner team triages/promotes (issues_promote moves it into a task on their board). The owner is notified automatically on your create.
        """;
}
