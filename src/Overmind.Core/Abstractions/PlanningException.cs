namespace Overmind.Core;

public sealed class PlanningException : Exception
{
    private PlanningException(
        PlanningFailureKind kind, PlanningFailureCode code, string message, string? fix = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        Code = code;
        Fix = fix;
    }

    public PlanningFailureKind Kind { get; }

    public PlanningFailureCode Code { get; }

    // One-line imperative remedy for state-conflict rejections; mechanical rejections leave it null.
    public string? Fix { get; }

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
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateSlug,
            $"A feature with slug '{slug}' already exists in this project.",
            "Resend planning_create_feature with a different slug — planning_list_features shows the slugs in use.");

    public static PlanningException DuplicateArtifact(PlanningArtifactKind kind, int order) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateArtifact,
            $"An artifact of kind '{kind}' with order {order} already exists.",
            "One artifact per kind and order — resend planning_upsert_artifact with the same kind and order to store the change as a new version.");

    public static PlanningException NumberCollision(
        string slug, string collidingSlug, PlanningFeatureStatus collidingStatus, int suggestedNumber) =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.NumberCollision,
            $"Feature number of '{slug}' is already taken by '{collidingSlug}' (status '{collidingStatus.ToString().ToLowerInvariant()}') " +
            "— NNN prefixes are the board's addressing layer and compare numerically (7 = 007). " +
            $"Rename the slug with the next free number: {suggestedNumber:D3}.",
            $"Rebuild the slug with the free number {suggestedNumber:D3} prefix and resend planning_create_feature.");

    public static PlanningException TaskBatchItem(int index, PlanningException inner) =>
        new(inner.Kind, inner.Code, $"tasks[{index}]: {inner.Message}", inner.Fix, inner);

    public static PlanningException BatchOp(int index, PlanningException inner) =>
        new(inner.Kind, inner.Code, $"ops[{index}]: {inner.Message}", inner.Fix, inner);

    public static PlanningException BatchEmpty() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BatchEmpty,
            "A batch must carry at least one op: an empty ops[] would commit nothing and still spend a materialization pass.");

    public static PlanningException BatchOpInvalid(string requirement) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidBatchOp, requirement);

    public static PlanningException BatchArtifactOutsideFeature(Guid artifactId, string featureSlug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.BatchArtifactOutsideFeature,
            $"Artifact '{artifactId}' does not belong to feature '{featureSlug}': batch ops stay on the batch feature — " +
            "the route's claim check covers that feature only.",
            $"Send batch ops only for artifacts of feature '{featureSlug}' — planning_list_artifacts lists them; move the foreign artifact to its own call.");

    public static PlanningException TaskBatchConcurrentWrite() =>
        new(PlanningFailureKind.Duplicate, PlanningFailureCode.DuplicateArtifact,
            "A concurrent writer claimed a task order while the batch was saving; the whole batch was rolled back — retry the call.",
            "Retry the planning_upsert_tasks call as-is — orders re-resolve on each attempt and nothing was stored.");

    public static PlanningException InvalidSlug(string slug) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidSlug, $"Slug '{slug}' does not match the required pattern (lowercase alphanumeric with hyphens, max 64 chars).");

    public static PlanningException ChangeNoteRequired() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ChangeNoteRequired,
            "A logNote is required to modify a validated artifact.",
            "Resend the same edit with a logNote explaining the change — validated artifacts edit only with a note.");

    public static PlanningException CannotValidateTask() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.CannotValidateTask,
            "Task artifacts cannot be validated.",
            "Tasks flow through planning_set_status (todo, inProgress, review, done, blocked), not through validation.");

    public static PlanningException CannotDeclineTask() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.CannotDeclineTask,
            "Decline does not apply to task artifacts; tasks carry their own status flow.");

    public static PlanningException InvalidStatusForKind(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.InvalidStatusForKind, $"Status '{status}' is not allowed for artifact kind '{kind}'.");

    public static PlanningException BriefNotAllowedForKind(PlanningArtifactKind kind) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.BriefNotAllowedForKind, $"A brief is only allowed on task artifacts; kind '{kind}' cannot carry one.");

    public static PlanningException AmendmentNotAllowedForKind(PlanningArtifactKind kind) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.AmendmentNotAllowedForKind,
            $"The amendment door appends to result artifacts only; kind '{kind}' is refused — " +
            "patch a section or edit and re-validate instead.");

    public static PlanningException AmendmentNoteRequired() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.AmendmentNoteRequired,
            "An amendment records the human's pre-approval in the log: pass the logNote carrying that provenance, e.g. 'human pre-approved via notification reply'.",
            "Resend the amendment with the logNote carrying the human's pre-approval provenance.");

    public static PlanningException AmendmentSectionPairRefused(string name) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.AmendmentSectionPairRefused,
            $"Amendment text must not carry an anchored '<{name}>…</{name}>' pair: sections are located by their " +
            "last wrapper, so a quoted pair would shadow the real section on reads and evict it from the mirror render. " +
            $"Quote it escaped instead ('&lt;{name}&gt;…&lt;/{name}&gt;') — a fenced code block does not protect, the grammar reads raw lines.");

    public static PlanningException InvalidBrief() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidBrief,
            $"A brief must be single-line text of at most {PlanningArtifact.BriefMaxLength} characters (a blank brief clears it).");

    public static PlanningException InvalidFeatureTransition(string slug, PlanningFeatureStatus current, PlanningFeatureStatus target) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidFeatureTransition,
            $"Feature '{slug}' cannot move from '{current}' to '{target}'; allowed targets: {string.Join(", ", PlanningFeatureTransitions.AllowedTargets(current).Select(s => s.ToString().ToLowerInvariant()))}.",
            "Resend planning_set_feature_status with one of the allowed targets listed in the detail; every other move belongs to the human UI door.");

    public static PlanningException FeatureLogNoteRequired() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureLogNoteRequired,
            "A logNote is required to move a feature status; it records who asked for the move.",
            "Resend planning_set_feature_status with a logNote carrying the provenance, e.g. 'by user request in-session'.");

    public static PlanningException FeatureStatusNotAllowedForAgent(PlanningFeatureStatus target) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureStatusNotAllowedForAgent,
            $"Agents may move a feature only to 'done' or 'inProgress'; '{target.ToString().ToLowerInvariant()}' is a manual move reserved for the human door in the Overmind UI.",
            $"Ask the human to set '{target.ToString().ToLowerInvariant()}' in the Overmind UI; the agent door planning_set_feature_status accepts only done and inProgress.");

    public static PlanningException HumanValidationRequired(PlanningArtifactKind kind, string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.HumanValidationRequired,
            $"STOP: the {kind.ToString().ToLowerInvariant()} of feature '{slug}' awaits human validation — ask the user to approve it in the Overmind UI.",
            $"Ask the human to validate the {kind.ToString().ToLowerInvariant()} of feature '{slug}' in the Overmind UI (or send overmind_notify(kind=approval, featureSlug={slug}) and wait for the reply), then re-read it before continuing.");

    public static PlanningException ValidationProofRequired(string featureSlug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ValidationProofRequired,
            "Human approval required: send overmind_notify(kind=approval, featureSlug=" + featureSlug +
            ") and wait for the human's reply in the notification center, or validate via the UI.",
            $"Send overmind_notify(kind=approval, featureSlug={featureSlug}), wait for a human reply newer than the artifact's last content update, then call planning_validate again — any content edit in between voids the reply.");

    public static PlanningException FeatureResultRequired(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureResultRequired,
            $"Feature '{slug}' cannot close as done: the result artifact is missing — write the result " +
            "first via planning_upsert_artifact(kind=result), or in the UI; the result is the readable " +
            "close-out a done flip requires.",
            $"Write the result first: planning_upsert_artifact(featureSlug={slug}, kind=result), then retry the done flip on planning_set_feature_status.");

    public static PlanningException FeatureDoneTapRequired(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureDoneTapRequired,
            $"done on '{slug}' needs the human's tap: send overmind_notify(kind=approval, featureSlug=" +
            slug + ") and wait for the reply in the notification center — the reply must be newer than " +
            "the result's last update; or let the human close the feature in the UI.",
            $"Send overmind_notify(kind=approval, featureSlug={slug}), wait for a human reply newer than the result's last update, then retry the done flip.");

    public static PlanningException MigratedNoteRequired() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.MigratedNoteRequired,
            "asMigrated requires a logNote: the marker line must record where the pre-approval came from, e.g. 'validated v9 on disk, user-approved before migration'.",
            "Resend planning_validate with asMigrated and a logNote recording the pre-approval provenance.");

    public static PlanningException ImportFailed(string source, Exception? inner = null) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.ImportFailed,
            $"Importing '{source}' failed: {inner?.Message ?? "unknown error"}", null, inner);

    public static PlanningException PatchSectionNotFound(
        string section, IEnumerable<string> presentSections, string? hint = null, string? acceptedSections = null)
    {
        var present = string.Join(", ", presentSections.Distinct());
        var message =
            $"Section '{section}' was not found in the artifact body. Sections present: {(present.Length == 0 ? "(none)" : present)}.";

        if (acceptedSections is not null)
        {
            message += $" Accepted sections for tasks: {acceptedSections}.";
        }

        return new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchSectionNotFound,
            hint is null ? message : $"{message} {hint}");
    }

    public static PlanningException PatchSectionNameNotParseable(string section) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchSectionNameNotParseable,
            $"Section name '{section}' does not match the tag grammar the parser reads: lowercase letters, digits and hyphens, starting with a letter (e.g. 'goal', 'anti-scope', 'my-tag'). " +
            "A section inserted under any other name could never be found again — rename it to a lowercase tag.");

    public static PlanningException PatchHeadingNotFound(string heading, IReadOnlyList<string> presentHeadings) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchHeadingNotFound,
            $"Heading '{heading}' was not found in the artifact body. H2 headings present: " +
            $"{(presentHeadings.Count == 0 ? "(none)" : string.Join(", ", presentHeadings.Select(h => $"'{h}'")))}. " +
            "Heading sections resolve case-sensitively by their exact text.");

    public static PlanningException PatchHeadingAmbiguous(string heading, IReadOnlyList<string> headings)
    {
        // 266/T3 wording upgrade, frequency leader of the crew's 400s: the count alone made people
        // hunt for duplicate headings that were never there. The rule the throw actually enforces
        // is that content is the block UNDER the heading and the heading line itself is
        // byte-stable — say that, keep the count and the merge-or-rename fallback honest.
        var bodyLine = headings.Count > 0 ? headings[0] : heading;
        return new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchHeadingAmbiguous,
            $"Heading '{heading}' matches {headings.Count} H2 headings in the body ({string.Join(", ", headings.Select(h => $"'{h}'"))}); " +
            "a patch would rewrite an arbitrary one — nothing was modified. " +
            $"The usual cause is the content repeating the heading line: a heading patch takes the block that sits under '{bodyLine}' " +
            "and the heading line itself stays byte-stable, so resend with that line stripped from the content. " +
            "If the body really does carry the heading twice, merge or rename the duplicates first.");
    }

    public static PlanningException PatchRemoveReasonRequired(string section) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.PatchRemoveReasonRequired,
            $"Tombstoning '{section}' requires a reason: the '<removed date — reason>' marker records why the block died, " +
            "and that reason is what keeps it from being silently resurrected. Resend with reason.");

    // 266/T3 row 9: the reason rides a one-line marker ('<removed yyyy-MM-dd — reason>'), so a
    // newline in it is not formatting but a broken record — no reader recognises the tombstone
    // any more. Same single-line rule the brief and the decline reason already carry.
    public static PlanningException RemoveReasonMultiline() =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.RemoveReasonMultiline,
            $"The tombstone reason must be single-line text of at most {SoftCapLimits.Reason} characters: it is written into " +
            "one '<removed yyyy-MM-dd — reason>' line, and a newline inside the reason makes that line unreadable — " +
            "nothing was modified. Resend with the reason collapsed to one line.");

    public static PlanningException PatchRemoveNotAllowedForXmlSection(string section) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.RemoveNotAllowedForXmlSection,
            $"remove=true resolves markdown heading sections on spec, plan and result bodies; '{section}' goes through " +
            "the XML-section path, whose tombstone semantics are not defined — tombstone a heading section instead, " +
            "or resend the full body without the section.");

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
            "The log section is append-only; replace_section cannot rewrite it. Append a dated line via planning_append_log or correct the newest own line via planning_edit_log; if history genuinely must be rewritten, resend the full body via planning_upsert_artifact.");

    public static PlanningException SectionContentRequiredForReplace(string section) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.SectionContentRequiredForReplace,
            $"Section '{section}' already exists — a content-less section patch only inserts a missing section; resend it with the replacement content.");

    public static PlanningException LogHistoryNotAppendable(int divergentIndex, int storedCount, int incomingCount) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LogHistoryNotAppendable,
            $"The log section is append-only: a resent body must carry the stored log lines as an exact ordered prefix, " +
            $"but log line {divergentIndex} is the first to diverge (stored {storedCount} lines, incoming {incomingCount}). " +
            "Append via planning_append_log, edit other sections via planning_patch_artifact, or resend the full body " +
            "with every stored log line untouched and new lines only at the end.");

    public static PlanningException LogBlocksDuplicated(int blockCount) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LogBlocksDuplicated,
            $"The body carries {blockCount} <log> blocks; only the last one is read, so lines in the earlier " +
            "blocks would silently vanish from the journal. Merge the history into a single <log> block and resend.");

    public static PlanningException DependencyCycle(string slug, string blockedBySlug, IReadOnlyList<string> chain) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.DependencyCycle,
            $"Adding '{blockedBySlug}' as a blocker of '{slug}' would create a dependency cycle: {string.Join(" -> ", chain)}.",
            $"Lift one existing edge of the chain {string.Join(" -> ", chain)} via planning_remove_feature_dependency, or close a blocker feature first — the new edge was not added.");

    public static PlanningException FeatureBlocked(string slug, IReadOnlyList<string> openBlockers) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureBlocked,
            $"STOP: feature '{slug}' is blocked by unclosed features: {string.Join(", ", openBlockers)}. " +
            "Pinning is refused — work a blocker first or ask the user to clear the link.",
            $"Work one of the blockers ({string.Join(", ", openBlockers)}) first via planning_current_task there, or ask the user before lifting the link with planning_remove_feature_dependency.");

    public static PlanningException FeatureNotInQueue(string slug) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.FeatureNotInQueue,
            $"Feature '{slug}' is done and no longer part of the queue; only non-done features can be moved.",
            "Pick a non-done feature — planning_task_queue lists the live queue.");

    public static PlanningException InvalidQueuePosition(string slug, int position, int bandSize) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidQueuePosition,
            $"Position {position} is outside the queue band of '{slug}': the band holds {bandSize} " +
            $"non-done features, so the valid range is 1..{bandSize}.",
            $"Resend planning_move_feature with toPosition in 1..{bandSize}, or use move up/down/first/last instead.");

    public static PlanningException InvalidQueueReorder(FeaturePriority priority, string discrepancy) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidQueueReorder,
            $"Reordering the '{priority.ToString().ToLowerInvariant()}' band failed: {discrepancy}",
            "Re-read planning_task_queue and resend the move against the current positions — the band shifted under this call.");

    public static PlanningException TaskDependencyCycle(int order, IReadOnlyList<int> chain) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.TaskDependencyCycle,
            $"Setting blockedBy on task order {order} would create a dependency cycle: {string.Join(" -> ", chain)}.",
            $"Re-upsert one task of the chain {string.Join(" -> ", chain)} via planning_upsert_artifact with a blockedBy list that drops one edge — the new edges were not saved.");

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
        string slug, string alias, string vendor, AgentSessionLiveness liveness)
    {
        var fix = liveness == AgentSessionLiveness.Alive
            ? $"Coordinate with {alias} by {vendor} in the feature's lane wave room and have the holder release via planning_unclaim_feature, or pick another feature — reads stay open to you."
            : $"The claim on '{slug}' is {liveness.ToString().ToLowerInvariant()}, not alive — resend planning_claim_feature to take it over after confirming {alias} is gone.";
        return new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureClaimHeld,
            $"Feature '{slug}': claim held by {alias} by {vendor} ({liveness.ToString().ToLowerInvariant()}). " +
            "The claim acts as a mutation lock: reads stay open, mutations and unclaims belong to the holder session — " +
            "the human door in the Overmind UI is never locked.", fix);
    }

    public static PlanningException FeatureClaimRefused(string slug) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.FeatureClaimRefused,
            $"Feature '{slug}' is done; claims are refused — reopen it first if work resumes.",
            $"Ask the human to reopen '{slug}' in the Overmind UI, then planning_claim_feature; until then pick a non-done feature.");

    public static PlanningException MissingIssue(Guid issueId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingIssue,
            $"Issue '{issueId}' was not found.");

    public static PlanningException IssueInvalidTransition(IssueStatus current, IssueStatus target) =>
        new(PlanningFailureKind.InvalidStatus, PlanningFailureCode.IssueInvalidTransition,
            $"Issue cannot move from '{current.ToString().ToLowerInvariant()}' to '{target.ToString().ToLowerInvariant()}'. " +
            "The forward chain is open → triaged → promoted → closed; closed is reachable from any non-closed state; " +
            "promoted is reached only through issues_promote; there is no reopen.",
            "Move forward only: triaged via issues_set_status, promoted only via issues_promote, closed via issues_set_status from any non-closed state; a re-found bug is a fresh issues_create.");

    public static PlanningException AgentDeletionRefused(PlanningArtifactKind kind, PlanningArtifactStatus status) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ArtifactDeletionRefused,
            $"The agent door deletes draft artifacts only: this {kind.ToString().ToLowerInvariant()} sits in status " +
            $"'{status.ToString().ToLowerInvariant()}'. Validated and done artifacts are deletable through the " +
            "human door in the Overmind UI.",
            "Ask the human to delete it in the Overmind UI; the agent door planning_delete_artifact covers drafts only.");

    public static PlanningException DeletionBlockedByDependents(IReadOnlyList<string> dependents) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.DeletionBlockedByDependents,
            "Clean up dependencies first: " +
            $"these tasks still depend on it: {string.Join(", ", dependents)}.",
            "Re-upsert each listed task via planning_upsert_artifact with a blockedBy list that drops this artifact, then retry planning_delete_artifact.");

    public static PlanningException ExportTargetNotAbsolute(string targetPath) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.ExportTargetNotAbsolute,
            $"Export target '{targetPath}' is not an absolute directory path.");

    public static PlanningException ExportTargetNotEmpty(string targetDirectory) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ExportTargetNotEmpty,
            $"Export target '{targetDirectory}' is not empty: export writes a fresh snapshot and refuses an existing tree.",
            "Point targetPath at an empty or not-yet-existing directory — export never syncs into an occupied tree.");

    public static PlanningException ExportTargetInsideMirror(string targetDirectory) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ExportTargetInsideMirror,
            $"Export target '{targetDirectory}' sits inside the materialized mirror (BACKLOG/ or ENTRIES/); " +
            "export never writes into the mirror itself.",
            "Choose a targetPath outside BACKLOG/ and ENTRIES/ — the mirror is Overmind-owned output.");

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
            "'pointer: <repo-relative-path>[#anchor]' or 'pointer: grimoire://memory/<id>' — the " +
            "case-sensitive marker 'pointer:' as the first token, whitespace, then either a repo-relative " +
            "path (no leading '/', forward slashes only, no '..' segments) with an optional non-empty " +
            "'#anchor', or a grimoire crew-memory id ('#' anchors don't apply to memory pointers). " +
            "Example: 'pointer: src/Overmind.Core/Entities/PlanningArtifact.cs#L81'.");

    public static PlanningException LogEditNotNewest(
        PlanningLogSource actor, PlanningLogSource? newestMarker, int ownedPosition, int lineCount) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditNotNewest,
            $"Only the newest log line is editable, and the newest line carries {MarkerLabel(newestMarker)}, " +
            $"not your [{actor.ToString().ToLowerInvariant()}] marker: your newest " +
            $"[{actor.ToString().ToLowerInvariant()}] line is entry {ownedPosition} of {lineCount}, and an edit " +
            "cannot reach past a newer line. Append a correction line via planning_append_log instead, " +
            "or let the newer line's door edit its own text.",
            "Append a correction line via planning_append_log — the edit reaches only the newest line and cannot cross a newer one.");

    public static PlanningException LogEditEmptyLog() =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditNotNewest,
            "The artifact's log holds no lines to edit: the edit reaches the newest line only — " +
            "append one via planning_append_log first.",
            "Append a first line via planning_append_log, then planning_edit_log can correct it.");

    public static PlanningException LogEditWrongActor(PlanningLogSource actor, PlanningLogSource? newestMarker) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LogEditWrongActor,
            $"The newest log line carries {MarkerLabel(newestMarker)}, and the log holds no " +
            $"[{actor.ToString().ToLowerInvariant()}] line to edit — only lines carrying your own door marker " +
            "are yours. Append a new line via planning_append_log instead.",
            "Append your correction as a new line via planning_append_log — only the newest line is editable and it belongs to another door.");

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
            $"A lane named '{name}' appears more than once in the roster; lane names are unique per project (case-insensitive).",
            "Drop the repeated entry from the lanes payload — the roster carries each name once.");

    public static PlanningException LaneRosterTooLarge(int count, int cap) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LaneRosterTooLarge,
            $"The roster carries {count} lanes; a project holds at most {cap}.");

    public static PlanningException InvalidLaneName(string name) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidLaneName,
            $"Lane name '{name}' is not addressable: '/', '\\', a bare '.' or '..' and control characters are refused, because the " +
            "edit and delete routes carry the name as a single path segment — a lane named that way would be " +
            "created but never addressable by those routes. Pick a name without those characters.");

    public static PlanningException LaneRosterWriteRetired(string retiredPath, string rosterPath) =>
        new(PlanningFailureKind.Gone, PlanningFailureCode.LaneRosterWriteRetired,
            $"'PUT {retiredPath}' is retired and wrote nothing: the whole-roster writer answers at 'PUT {rosterPath}'. " +
            "It left the collection on purpose — a client removes a '.' segment before the wire (RFC 3986 reference " +
            "resolution), so a URL whose shape says one-lane surgery arrived as a PUT on the collection and swapped " +
            $"the entire roster. Resend a wholesale write to 'PUT {rosterPath}', " +
            $"or edit one lane at 'PUT {retiredPath}/{{name}}'.",
            $"Resend the full roster to 'PUT {rosterPath}', or address the single lane at its granular PUT route by name.");

    public static PlanningException LaneNotInRoster(string lane, IReadOnlyList<string> rosterNames) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneNotInRoster,
            $"Lane '{lane}' is not in the project roster; features join a lane only by roster name. " +
            $"Roster present: {(rosterNames.Count == 0 ? "(empty)" : string.Join(", ", rosterNames))}. " +
            "Extend the roster via planning_set_lanes first.",
            "Birth the lane first via planning_create_lane, then reassign the feature.");

    public static PlanningException LaneBirthRestricted(string name) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.LaneBirthRestricted,
            $"Lane '{name}' is not in the roster and set_lanes no longer births lanes — a payload may only " +
            "reshape lanes that already exist, and an empty payload is a no-op. Use planning_create_lane " +
            "(or assignment via planning_update_feature) as the birth door.",
            $"Call planning_create_lane(name={name}) as the birth door, or assign the lane via planning_update_feature; set_lanes only reshapes known lanes.");

    public static PlanningException LaneStillOccupied(string lane, IReadOnlyList<string> slugs) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneStillOccupied,
            $"Lane '{lane}' still holds open features: {string.Join(", ", slugs)}. Removal is refused — " +
            "close them as done or move them to another lane first; a lane with work in flight cannot be dropped.",
            $"Move the listed features ({string.Join(", ", slugs)}) to another lane via planning_update_feature's lane field or close them done, then retry planning_remove_lane.");

    public static PlanningException LaneClaimHeld(
        string lane, string slug, string alias, string vendor, AgentSessionLiveness liveness) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.LaneClaimHeld,
            $"Lane '{lane}' is busy: feature '{slug}' holds an alive claim by {alias} by {vendor} " +
            $"({liveness.ToString().ToLowerInvariant()}). One agent owns a lane at a time — " +
            "pick a feature outside this lane, or resend with overrideLaneCheck=true to claim anyway.",
            $"Pick a feature outside lane '{lane}', coordinate with {alias} in the lane wave room, or resend planning_claim_feature with overrideLaneCheck=true to take the lane knowingly.");

    public static PlanningException MissingLane(string name) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingLane,
            $"Lane '{name}' was not found in this project's roster — lane names are the identity and are matched " +
            "case-insensitively, so this is either a lane that was never created or one that was already deleted; " +
            "planning_list_lanes shows the roster that is live now.");

    public static PlanningException MissingWaveRoom(string room, IReadOnlyList<string> rosterNames) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingWaveRoom,
            $"Wave room '{room}' does not exist in this project: rooms are the lane roster plus the shared 'all' room. " +
            $"Rooms live now: {(rosterNames.Count == 0 ? "'all' only (the roster is empty)" : string.Join(", ", rosterNames.Select(n => $"'{n}'")))}.");

    public static PlanningException WaveTextTooLong(int length, int cap) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.WaveTextTooLong,
            $"text is {length} chars; cap is {cap}; cut {length - cap}");

    public static PlanningException MissingConversation(Guid conversationId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingConversation,
            $"Conversation '{conversationId}' was not found.");

    public static PlanningException MissingConversationMessage(Guid messageId, Guid conversationId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingConversationMessage,
            $"Message '{messageId}' was not found in conversation '{conversationId}'.");

    public static PlanningException ConversationTextTooLong(int length, int cap) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.ConversationTextTooLong,
            $"Conversation message text is {length} characters; the cap is {cap}.");

    public static PlanningException DirectiveTextTooLong(int actual, int limit, int ceiling, string? lostTailPreview) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.DirectiveTextTooLong,
            $"Directive text is {actual} characters; the limit is {limit} and the accept ceiling is {ceiling}. " +
            "Nothing was stored: a directive is a command, so this door never truncates. " +
            $"The tail that would have been lost reads \"{lostTailPreview}\". " +
            "Compress below the ceiling and resend.",
            "Split the command into two planning_append_directive calls each under the ceiling, or compress it below the ceiling and resend — a directive is never cut.");

    public static PlanningException MissingDirective(Guid directiveId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingDirective,
            $"Directive '{directiveId}' was not found on this feature — it may live on another feature, " +
            "or it was never appended; planning_read_feature lists the open directives with their ids.");

    public static PlanningException MissingEntry(Guid entryId) =>
        new(PlanningFailureKind.NotFound, PlanningFailureCode.MissingEntry,
            $"Entry '{entryId}' was not found in this project — it may belong to another project, " +
            "or it was never written; list_entries lists the project's entries with their ids.");

    public static PlanningException InvalidOperationId(string raw) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.InvalidOperationId,
            $"The 'X-Operation-Id' header must carry a GUID; '{raw}' does not parse as one. " +
            "Omit the header for fire-and-forget writes, or send a fresh GUID per logical operation.");

    public static PlanningException OperationIdConflict(Guid operationId) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.OperationIdConflict,
            $"Operation id '{operationId}' was already used with a different request (method, path or body differ). " +
            "An operation id replays only its own request: resend the identical payload to get the stored response, " +
            "or mint a new id for a different operation.",
            "Resend the identical payload under the same operationId to replay the stored response, or mint a fresh operationId for a different operation.");

    public static PlanningException ArtifactOutsideCallerProject(
        Guid artifactId, Guid owningProjectId, Guid callerProjectId) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.ArtifactOutsideCallerProject,
            $"Artifact '{artifactId}' belongs to project '{owningProjectId}', but this conversation belongs to project " +
            $"'{callerProjectId}'. The host compared the object you addressed with the conversation's project and " +
            "refused the call, so nothing was read or written and the artifact's content stayed on its own board. " +
            $"Address an artifact of '{callerProjectId}' — planning_list_artifacts lists that project's features — " +
            $"or say to the owner that this work belongs to '{owningProjectId}'.",
            $"Address an artifact of this conversation's project '{callerProjectId}' (planning_list_artifacts lists them), or hand the work to the owner of '{owningProjectId}' — do not retry across boards.");

    public static PlanningException IssueOutsideCallerProject(
        Guid issueId, Guid owningProjectId, Guid callerProjectId) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.IssueOutsideCallerProject,
            $"Issue '{issueId}' belongs to project '{owningProjectId}', but this conversation belongs to project " +
            $"'{callerProjectId}'. The host compared the object you addressed with the conversation's project and " +
            "refused the call, so nothing was read or written and the issue stayed on its own board. " +
            $"Address an issue of '{callerProjectId}' — issues_list reads that board — or file a request on " +
            $"'{owningProjectId}' with issues_create, which is the door that may write to another board while " +
            "presenting your own project.",
            $"To ask for the fix on '{owningProjectId}', file it there with issues_create instead of editing that board's issue from this conversation.");

    public static PlanningException EntryOutsideCallerProject(
        Guid entryId, Guid owningProjectId, Guid callerProjectId) =>
        new(PlanningFailureKind.RuleViolation, PlanningFailureCode.EntryOutsideCallerProject,
            $"Entry '{entryId}' belongs to project '{owningProjectId}', but this conversation belongs to project " +
            $"'{callerProjectId}'. The host compared the object you addressed with the conversation's project and " +
            "refused the call, so nothing was read, changed or deleted and the entry stayed on its own board. " +
            $"Address an entry of '{callerProjectId}' — list_entries reads that project's notes, decisions, gates " +
            "and guards. Another project's context is not this conversation's to edit; if the knowledge belongs " +
            $"there, say so to the owner of '{owningProjectId}'.",
            $"Address an entry of this conversation's project '{callerProjectId}' via list_entries, or tell the owner of '{owningProjectId}' — another project's context is not editable from here.");

    public static PlanningException AgentCallerScopeUnverifiable(string defect) =>
        new(PlanningFailureKind.InvalidInput, PlanningFailureCode.AgentCallerScopeUnverifiable,
            $"The caller identity is unverifiable: {defect}. The host cannot tell which project this conversation " +
            "belongs to, and refusing beats guessing — nothing was read or written. The agent marker and the caller " +
            "scope travel only as a pair, emitted together by the embedded-agent transport and never assembled by " +
            "hand; a conversation without a project still sends the pair, with the scope written as the literal " +
            "'global'.",
            "Re-run overmind_onboard so the session registers its project scope (set OVERMIND_PROJECT_ID for worktree layouts), then retry — the marker pair is transport-emitted, never hand-assembled.");
}
