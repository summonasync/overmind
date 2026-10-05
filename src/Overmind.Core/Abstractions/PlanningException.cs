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
            $"The text '{text}' matches {matchingIndices.Count} acceptance-criteria items (indices {string.Join(", ", matchingIndices)}); tick by index instead.");

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

    private static string FormatAcItems(IReadOnlyList<PlanningAcItem> items) =>
        string.Join("; ", items.Select(i => $"{i.Index}: {i.RawText}"));
}
