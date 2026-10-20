namespace Overmind.Core;

public sealed class PlanningException : Exception
{
    private PlanningException(PlanningFailureKind kind, PlanningFailureCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        Code = code;
    }

    public PlanningFailureKind Kind { get; }

    public PlanningFailureCode Code { get; }

    public static PlanningException MissingProject(Guid projectId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingProject, $"Project '{projectId}' was not found.");

    public static PlanningException MissingFeature(string identifier) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingFeature, $"Feature '{identifier}' was not found.");

    public const string ArtifactNotFoundHint =
        "Addressing by slug? The MCP tools accept artifact aliases: '<slug>/T<order>' for a task, " +
        "'<slug>/S', '<slug>/P', '<slug>/R' for the single spec, plan or result — " +
        "planning_list_artifacts lists the feature's actual kind/order pairs.";

    public static PlanningException MissingArtifact(Guid artifactId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingArtifact,
            $"Artifact '{artifactId}' was not found. {ArtifactNotFoundHint}");

    public static PlanningException DuplicateSlug(string slug) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateSlug, $"A feature with slug '{slug}' already exists in this project.");

    public static PlanningException DuplicateArtifact(PlanningArtifactKind kind, int order) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateArtifact, $"An artifact of kind '{kind}' with order {order} already exists.");

    public static PlanningException NumberCollision(
        string slug, string collidingSlug, PlanningFeatureStatus collidingStatus, int suggestedNumber) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.NumberCollision,
            $"Feature number of '{slug}' is already taken by '{collidingSlug}' (status '{collidingStatus.ToString().ToLowerInvariant()}') " +
            "— NNN prefixes are the board's addressing layer and compare numerically (7 = 007). " +
            $"Rename the slug with the next free number: {suggestedNumber:D3}.");

    public static PlanningException TaskBatchItem(int index, PlanningException inner) =>
        new(inner.Kind, inner.Code, $"tasks[{index}]: {inner.Message}", inner);

    public static PlanningException TaskBatchConcurrentWrite() =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateArtifact,
            "A concurrent writer claimed a task order while the batch was saving; the whole batch was rolled back — retry the call.");

    public static PlanningException InvalidSlug(string slug) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidSlug, $"Slug '{slug}' does not match the required pattern (lowercase alphanumeric with hyphens, max 64 chars).");

    public static PlanningException ChangeNoteRequired() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ChangeNoteRequired, "A logNote is required to modify a validated artifact.");

    public static PlanningException CannotValidateTask() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.CannotValidateTask, "Task artifacts cannot be validated.");

    public static PlanningException CannotDeclineTask() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.CannotDeclineTask,
            "Decline does not apply to task artifacts; tasks carry their own status flow.");

    public static PlanningException InvalidStatusForKind(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.InvalidStatusForKind, $"Status '{status}' is not allowed for artifact kind '{kind}'.");

    public static PlanningException BriefNotAllowedForKind(PlanningArtifactKind kind) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BriefNotAllowedForKind, $"A brief is only allowed on task artifacts; kind '{kind}' cannot carry one.");

    public static PlanningException InvalidBrief() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidBrief,
            $"A brief must be single-line text of at most {PlanningArtifact.BriefMaxLength} characters (a blank brief clears it).");

    public static PlanningException InvalidFeatureTransition(string slug, PlanningFeatureStatus current, PlanningFeatureStatus target) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidFeatureTransition,
            $"Feature '{slug}' cannot move from '{current}' to '{target}'; allowed targets: {string.Join(", ", PlanningFeatureTransitions.AllowedTargets(current).Select(s => s.ToString().ToLowerInvariant()))}.");

    public static PlanningException FeatureLogNoteRequired() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureLogNoteRequired,
            "A logNote is required to move a feature status; it records who asked for the move.");

    public static PlanningException FeatureStatusNotAllowedForAgent(PlanningFeatureStatus target) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureStatusNotAllowedForAgent,
            $"Agents may move a feature only to 'done' or 'inProgress'; '{target.ToString().ToLowerInvariant()}' is a manual move reserved for the human door in the Overmind UI.");

    public static PlanningException HumanValidationRequired(PlanningArtifactKind kind, string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.HumanValidationRequired,
            $"STOP: the {kind.ToString().ToLowerInvariant()} of feature '{slug}' awaits human validation — ask the user to approve it in the Overmind UI.");

    public static PlanningException ValidationProofRequired(string featureSlug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ValidationProofRequired,
            "Human approval required: send overmind_notify(kind=approval, featureSlug=" + featureSlug +
            ") and wait for the human's reply in the notification center, or validate via the UI.");

    public static PlanningException FeatureResultRequired(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureResultRequired,
            $"Feature '{slug}' cannot close as done: the result artifact is missing — write the result " +
            "first via planning_upsert_artifact(kind=result), or in the UI; the result is the readable " +
            "close-out a done flip requires.");

    public static PlanningException FeatureDoneTapRequired(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureDoneTapRequired,
            $"done on '{slug}' needs the human's tap: send overmind_notify(kind=approval, featureSlug=" +
            slug + ") and wait for the reply in the notification center — the reply must be newer than " +
            "the result's last update; or let the human close the feature in the UI.");

    public static PlanningException MigratedNoteRequired() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.MigratedNoteRequired,
            "asMigrated requires a logNote: the marker line must record where the pre-approval came from, e.g. 'validated v9 on disk, user-approved before migration'.");

    public static PlanningException ImportFailed(string source, Exception? inner = null) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.ImportFailed,
            $"Importing '{source}' failed: {inner?.Message ?? "unknown error"}", inner);

    public static PlanningException PatchSectionNotFound(
        string section, IEnumerable<string> presentSections, string? hint = null)
    {
        var present = string.Join(", ", presentSections.Distinct());
        var message =
            $"Section '{section}' was not found in the artifact body. Sections present: {(present.Length == 0 ? "(none)" : present)}.";

        return new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchSectionNotFound,
            hint is null ? message : $"{message} {hint}");
    }

    public static string InlineSectionHint(string section) =>
        $"A one-line '<{section}>…</{section}>' section exists in the body — one-line sections need " +
        "the canonical multi-line form; re-send the body once (any upsert canonicalizes it).";

    public static PlanningException TickItemNotFoundByIndex(int index, IReadOnlyList<PlanningAcItem> visibleItems) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.TickItemNotFound,
            visibleItems.Count == 0
                ? $"The artifact body contains no acceptance-criteria items; there is no item {index}."
                : $"Acceptance-criteria item {index} was not found. Visible items: {FormatAcItems(visibleItems)}.");

    public static PlanningException TickItemNotFoundByText(string text, IReadOnlyList<PlanningAcItem> visibleItems) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.TickItemNotFound,
            visibleItems.Count == 0
                ? $"The artifact body contains no acceptance-criteria items; nothing matches '{text}'."
                : $"No acceptance-criteria item matches the exact text '{text}'. Visible items: {FormatAcItems(visibleItems)}.");

    public static PlanningException TickTextAmbiguous(string text, IReadOnlyList<int> matchingIndices) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.TickTextAmbiguous,
            $"The text '{text}' matches {matchingIndices.Count} acceptance-criteria items (indices {string.Join(", ", matchingIndices)}); address by index instead.");

    public static PlanningException TickItemNotCheckable(int index, string rawText) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.TickItemNotCheckable,
            $"Acceptance-criteria item {index} has no checkbox marker and cannot be ticked or unticked: '{rawText}'.");

    public static PlanningException InvalidPatchRequest() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidPatchRequest,
            "A patch must carry exactly one of: a section with its replacement content, an acceptance-criteria tick or un-tick (index or exact text), or a brief — combinations are rejected.");

    public static PlanningException PatchLogSectionNotReplaceable() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchLogSectionNotReplaceable,
            "The log section is append-only; replace_section cannot rewrite it. Append a dated line via planning_append_log or the artifact log endpoint; if history genuinely must be rewritten, resend the full body via planning_upsert_artifact.");

    public static PlanningException LogHistoryNotAppendable(int divergentIndex, int storedCount, int incomingCount) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LogHistoryNotAppendable,
            $"The log section is append-only: a resent body must carry the stored log lines as an exact ordered prefix, " +
            $"but log line {divergentIndex} is the first to diverge (stored {storedCount} lines, incoming {incomingCount}). " +
            "Append via planning_append_log, edit other sections via planning_patch_artifact, or resend the full body " +
            "with every stored log line untouched and new lines only at the end.");

    public static PlanningException DependencyCycle(string slug, string blockedBySlug, IReadOnlyList<string> chain) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.DependencyCycle,
            $"Adding '{blockedBySlug}' as a blocker of '{slug}' would create a dependency cycle: {string.Join(" -> ", chain)}.");

    public static PlanningException FeatureBlocked(string slug, IReadOnlyList<string> openBlockers) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureBlocked,
            $"STOP: feature '{slug}' is blocked by unclosed features: {string.Join(", ", openBlockers)}. " +
            "Pinning is refused — work a blocker first or ask the user to clear the link.");

    public static PlanningException FeatureNotInQueue(string slug) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureNotInQueue,
            $"Feature '{slug}' is done and no longer part of the queue; only non-done features can be moved.");

    public static PlanningException InvalidQueuePosition(string slug, int position, int bandSize) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidQueuePosition,
            $"Position {position} is outside the queue band of '{slug}': the band holds {bandSize} " +
            $"non-done features, so the valid range is 1..{bandSize}.");

    public static PlanningException InvalidQueueReorder(FeaturePriority priority, string discrepancy) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidQueueReorder,
            $"Reordering the '{priority.ToString().ToLowerInvariant()}' band failed: {discrepancy}");

    public static PlanningException TaskDependencyCycle(int order, IReadOnlyList<int> chain) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.TaskDependencyCycle,
            $"Setting blockedBy on task order {order} would create a dependency cycle: {string.Join(" -> ", chain)}.");

    public static PlanningException BlockedByOrderUnknown(int order, IReadOnlyList<int> taskOrders) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BlockedByOrderUnknown,
            $"blockedBy order {order} does not match any task of the feature. Task orders present: " +
            $"{(taskOrders.Count == 0 ? "(none)" : string.Join(", ", taskOrders))}.");

    public static PlanningException BlockedBySelfReference(int order) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BlockedBySelfReference,
            $"Task order {order} cannot block itself.");

    public static PlanningException BlockedByDuplicateOrder(int order) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BlockedByDuplicateOrder,
            $"blockedBy lists order {order} more than once.");

    public static PlanningException BlockedByNotAllowedForKind(PlanningArtifactKind kind) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BlockedByNotAllowedForKind,
            $"blockedBy is only allowed on task artifacts; kind '{kind}' cannot carry one.");

    public static PlanningException FeatureClaimHeld(
        string slug, string alias, string vendor, AgentSessionLiveness liveness) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureClaimHeld,
            $"Feature '{slug}': claim held by {alias} by {vendor} ({liveness.ToString().ToLowerInvariant()}). " +
            "The claim acts as a mutation lock: reads stay open, mutations and unclaims belong to the holder session — " +
            "the human door in the Overmind UI is never locked.");

    public static PlanningException FeatureClaimRefused(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureClaimRefused,
            $"Feature '{slug}' is done; claims are refused — reopen it first if work resumes.");

    public static PlanningException MissingIssue(Guid issueId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingIssue,
            $"Issue '{issueId}' was not found.");

    public static PlanningException IssueInvalidTransition(IssueStatus current, IssueStatus target) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.IssueInvalidTransition,
            $"Issue cannot move from '{current.ToString().ToLowerInvariant()}' to '{target.ToString().ToLowerInvariant()}'. " +
            "The forward chain is open → triaged → promoted → closed; closed is reachable from any non-closed state; " +
            "promoted is reached only through issues_promote; there is no reopen.");

    public static PlanningException AgentDeletionRefused(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ArtifactDeletionRefused,
            $"The agent door deletes draft artifacts only: this {kind.ToString().ToLowerInvariant()} sits in status " +
            $"'{status.ToString().ToLowerInvariant()}'. Validated and done artifacts are deletable through the " +
            "human door in the Overmind UI.");

    public static PlanningException DeletionBlockedByDependents(IReadOnlyList<string> dependents) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.DeletionBlockedByDependents,
            "Clean up dependencies first: " +
            $"these tasks still depend on it: {string.Join(", ", dependents)}.");

    public static PlanningException ExportTargetNotAbsolute(string targetPath) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.ExportTargetNotAbsolute,
            $"Export target '{targetPath}' is not an absolute directory path.");

    public static PlanningException ExportTargetNotEmpty(string targetDirectory) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ExportTargetNotEmpty,
            $"Export target '{targetDirectory}' is not empty: export writes a fresh snapshot and refuses an existing tree.");

    public static PlanningException ExportTargetInsideMirror(string targetDirectory) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ExportTargetInsideMirror,
            $"Export target '{targetDirectory}' sits inside the materialized mirror (BACKLOG/ or ENTRIES/); " +
            "export never writes into the mirror itself.");

    public static PlanningException ArtifactVersionNotFound(Guid artifactId, int version, int currentVersion)
    {
        var coverage = currentVersion <= 1
            ? "version 1 is the live body itself; no snapshots are stored yet"
            : $"the live body is version {currentVersion}; stored snapshots cover 1..{currentVersion - 1}";

        return new(PlanningFailureKind.NotFound, PlanningFailureCode.ArtifactVersionNotFound,
            $"Artifact '{artifactId}' has no version {version}: {coverage}.");
    }

    public static PlanningException InvalidVersionRange(int fromVersion, int toVersion) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidVersionRange,
            fromVersion == toVersion
                ? $"Invalid version range: from and to are both {fromVersion}; there is nothing to diff."
                : $"Invalid version range: from ({fromVersion}) is greater than to ({toVersion}); " +
                "versions diff forward, from oldest to newest.");

    public static PlanningException InvalidPointerMarker(string line, string rule) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidPointerMarker,
            $"Malformed pointer line '{line}': {rule} A pointer line is " +
            "'pointer: <repo-relative-path>[#anchor]' — the case-sensitive marker 'pointer:' as the " +
            "first token, whitespace, then a repo-relative path (no leading '/', forward slashes only, " +
            "no '..' segments) with an optional non-empty '#anchor'.");

    public static PlanningException LogEditNotNewest(
        PlanningLogSource actor, PlanningLogSource? newestMarker, int ownedPosition, int lineCount) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditNotNewest,
            $"Only the newest log line is editable, and the newest line carries {MarkerLabel(newestMarker)}, " +
            $"not your [{actor.ToString().ToLowerInvariant()}] marker: your newest " +
            $"[{actor.ToString().ToLowerInvariant()}] line is entry {ownedPosition} of {lineCount}, and an edit " +
            "cannot reach past a newer line. Append a correction line via planning_append_log instead, " +
            "or let the newer line's door edit its own text.");

    public static PlanningException LogEditEmptyLog() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditNotNewest,
            "The artifact's log holds no lines to edit: the edit reaches the newest line only — " +
            "append one via planning_append_log first.");

    public static PlanningException LogEditWrongActor(PlanningLogSource actor, PlanningLogSource? newestMarker) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditWrongActor,
            $"The newest log line carries {MarkerLabel(newestMarker)}, and the log holds no " +
            $"[{actor.ToString().ToLowerInvariant()}] line to edit — only lines carrying your own door marker " +
            "are yours. Append a new line via planning_append_log instead.");

    public static PlanningException LogEditBlankText() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LogEditBlankText,
            "The replacement text of a log line must not be blank: the edit corrects the line's prose, it never removes the line.");

    public static PlanningException LogEditMultilineText() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LogEditMultilineText,
            "Log line text must be single-line; the log grammar is line-based — text carrying '\\r' or '\\n' " +
            "would splice verbatim into the body and parse as extra log lines.");

    private static string MarkerLabel(PlanningLogSource? source) =>
        source is { } marker
            ? $"the [{marker.ToString().ToLowerInvariant()}] marker"
            : "no actor marker";

    private static string FormatAcItems(IReadOnlyList<PlanningAcItem> items) =>
        string.Join("; ", items.Select(i => $"{i.Index}: {i.RawText}"));

    public static PlanningException LaneNameDuplicate(string name) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.LaneNameDuplicate,
            $"A lane named '{name}' appears more than once in the roster; lane names are unique per project (case-insensitive).");

    public static PlanningException LaneRosterTooLarge(int count, int cap) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LaneRosterTooLarge,
            $"The roster carries {count} lanes; a project holds at most {cap}.");

    public static PlanningException InvalidLaneName(string name) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidLaneName,
            $"Lane name '{name}' is not addressable: '/', '\\', a bare '.' or '..' and control characters are refused, because the " +
            "edit and delete routes carry the name as a single path segment — a lane named that way would be " +
            "created but never addressable by those routes. Pick a name without those characters.");

    public static PlanningException LaneNotInRoster(string lane, IReadOnlyList<string> rosterNames) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneNotInRoster,
            $"Lane '{lane}' is not in the project roster; features join a lane only by roster name. " +
            $"Roster present: {(rosterNames.Count == 0 ? "(empty)" : string.Join(", ", rosterNames))}. " +
            "Extend the roster via planning_set_lanes first.");

    public static PlanningException LaneClaimHeld(
        string lane, string slug, string alias, string vendor, AgentSessionLiveness liveness) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneClaimHeld,
            $"Lane '{lane}' is busy: feature '{slug}' holds an alive claim by {alias} by {vendor} " +
            $"({liveness.ToString().ToLowerInvariant()}). One agent owns a lane at a time — " +
            "pick a feature outside this lane, or resend with overrideLaneCheck=true to claim anyway.");

    public static PlanningException MissingLane(string name) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingLane,
            $"Lane '{name}' was not found in this project's roster — lane names are the identity and are matched " +
            "case-insensitively, so this is either a lane that was never created or one that was already deleted; " +
            "planning_list_lanes shows the roster that is live now.");

    public static PlanningException MissingDirective(Guid directiveId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingDirective,
            $"Directive '{directiveId}' was not found on this feature — it may live on another feature, " +
            "or it was never appended; planning_read_feature lists the open directives with their ids.");

    public static PlanningException LaneSerialBusy(string lane, string openSlug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneSerialBusy,
            $"Lane '{lane}' is serial: feature '{openSlug}' is still open in this lane. " +
            "One feature at a time within a lane — finish it first, or resend with " +
            "overrideLaneCheck=true to claim anyway.");

    public static PlanningException MissingEntry(Guid entryId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingEntry,
            $"Entry '{entryId}' was not found in this project — it may belong to another project, " +
            "or it was never written; list_entries lists the project's entries with their ids.");
}
