# MCP Tool Contracts (Tier 2)

Full per-tool contracts for the Overmind MCP adapter. Tool and parameter `[Description]`
attributes carry only selection-critical prose (budget: ≤400 chars per tool description,
≤120 per parameter, ≤800 per tool, ≤24,000 repo-wide, enforced by `DescriptionBudgetLintTests`).
Everything the description dropped — field enumerations, paging recipes, error behavior,
resolution mechanics — lives here. One section per tool; the section heading is exactly the
tool name, and that heading is the anchor (`docs/mcp-tool-contracts.md#<tool-name>`).
Planning ceremony mechanics stay in the `overmind-ceremony` skill; this doc is the wire contract.

## overmind_wake_peek

Wake ritual for the calling session: a side-effect-free read of the two pending-signal arms.
Advances no watermarks, marks nothing read — safe to poll any time.

**Response.** `{signals, cursor}`. Each signal: `{kind, refId, roomId, from, createdAt, excerpt}`.

- `notification-reply` — a human reply to a notification this session sent. `refId` is the
  notification id, `roomId` is null, `from` is `human`, `createdAt` is the notification's
  `RepliedAt`, `excerpt` is the reply text capped at 200 text elements.
- `wave-mention` — a wave chat message mentioning this session's alias. `roomId` is the wave
  room, `from` is the author alias. `refId` is the message id.

**Default windows (omit `since`).**

- Replies: notifications carrying `ReplyText` whose `readAt` is null — a consumed reply leaves
  the default window immediately. The window looks back 7 days; override with
  `OVERMIND_WAKE_REPLY_LOOKBACK_DAYS` on the host (values below 1 clamp to 1, values above 365
  clamp to 365 so one env typo cannot reach a `TimeSpan` overflow). The mention arm never reads
  that variable.
- Mentions: messages created after the session's `LastSeenAt` as it stood before this request.

**Explicit `since` is the replay override.** When `since` is present (a bare timestamp or a
cursor echo), the reply arm also returns consumed (`readAt`) notifications in the window —
that is how replayed or paged traffic looks complete. The mention arm bounds at the same instant.

**Project scoping.** The adapter appends `projectId=<resolved>` to the wake request whenever its
own ProjectResolver yields one (env `OVERMIND_PROJECT_ID`, project pin, or host onboard reply —
resolution failure is swallowed and keeps the poll machine-wide). A session that resolves none
gets the machine-wide window. When scoped, the reply arm filters to notifications of that project;
foreign-project replies stop showing up. The mention arm has no project filter.

**Paging.** ≤100 signals per page, ordered by `createdAt` asc, ties broken by `refId`. The cursor
is the newest `createdAt` seen followed by `'|'` and the newest signal's `refId`, or the request
`since` on an empty page. Feed the cursor back verbatim as `since` and repeat until an under-cap
page arrives; the cursor is inclusive-exact, so no loss or duplicates across a tie group.

**Session identity.** The adapter stamps its registered session automatically; a headerless or
unknown session fails with a named error (`POST /api/agent/sessions` registers).

**Host wire shape.** `GET /api/agent/wake` with optional `since` (ISO 8601, optionally
`|<refId>` — malformed values return 400 naming the expected shape) and optional `projectId`
(adapter-internal; not a tool parameter). The response JSON above is the agent-facing contract
and is unchanged by the lookback, consumption and scoping work; only the row set moved.

<!-- append-regions: the two diet batches fill disjoint zones of this file so parallel lanes
     never collide on the same lines. Insert a tool's section INSIDE its own region, above its
     end marker; never move or rename a marker, never add a heading outside a region.
     region:planning  -> planning_* tools (diet batch A)
     region:others    -> every non-planning, non-wake tool (diet batch B) -->
<!-- region:planning:start -->

## planning_list_features

The full feature board of one project. Read-only and idempotent.

**Rows.** `slug`, `title`, `status`, `priority`, `lane`, `specStatus`/`planStatus`, task progress
(`taskTotal`, `taskDone`, `taskCounts`: todo/inProgress/review/done/blocked — the parts sum to
`taskTotal`), `createdAt`, `updatedAt`, and on claimed entries the claim block
(`claimedByAlias`, `claimedByVendor`, `claimedAt`, `claimState` alive | stale | released).
`released` means the holder session ended; `stale` and `released` claims are take-over-able
through `planning_claim_feature`.

**Filters** (AND, all additive — omit everything for the unchanged full board): `status`
(draft | ready | inProgress | done | review), `priority` (p0-p3), `lane` (case-insensitive
roster name), `claimedByAlias` (exact stored claim owner, any liveness), `unclaimedOnly`
(features with no claim record).

**Projection knobs**, mutually exclusive: `fields` collapses each row to
`{slug, ...requested}` in request order — unknown keys are rejected before the call naming the
allowed set, and a list naming no keys is a lint error; `compact` returns the predefined
usual-shape row (identity, phase, progress, claim state). Omitting both returns the full rows.

## planning_read_feature

One feature plus every spec, plan, task and result artifact, with statuses and log lines.
The most expensive read on the board. Read-only and idempotent.

**Narrowing.** `sections` limits the feature body to the named sections, returned in body order;
`includeArtifacts=false` returns the body plus a roster summary (kind, order, status, brief for
tasks, `acTicked`/`acTotal`) without artifact bodies or log lines.
`planning_list_artifacts` is the cheaper roster, `planning_read_artifact` one body.

**Payload.** The claim block appears when the feature is claimed
(`claimedByAlias`, `claimedByVendor`, `claimedAt`, `claimState` alive | stale | released).
`review` appears on task payloads only.

**Projection knobs**, mutually exclusive: `compact` returns the predefined cheap shape — scalars
and the claim block stay, the body collapses to `bodyChars`, artifacts become roster rows (kind,
order, status, brief, ticks when the payload carries them) with logs collapsed to the last line,
composing with `sections` and `includeArtifacts`; `fields` projects the top-level payload keys
to `{slug, ...requested}` — unknown keys are rejected naming the allowed set, a list naming no
keys is a lint error. Omitting both returns the full read, byte-faithful to storage.

## planning_list_artifacts

Metadata-only roster of one feature's artifacts. Read-only.

**Rows.** id, kind, order, status, version and brief/tick counts per artifact. `kind` filters to
spec, plan, task or result; the server validates the value and rejects unknown ones with a 400.

**Pointers.** Every row carries additive `pointers: [{path, anchor}]` parsed from `pointer:`
lines in the body — an artifact can point at a repo file instead of copying it. The server never
reads the targets; the array is empty when none.

## planning_current_task

The task a coder should take now, or a stop/held/empty state whose reason leads the payload.
Read-only.

**States.** A feature with unclosed blockers returns a stop state naming them — reads stay open,
only task issuance stops. `held` means every remaining task sits in review. The all-done states
tell you to write the result artifact, validate a drafted one, or close the feature after
validation. Task payloads carry soft spec/plan warnings, never a STOP.

**Order.** Tasks are picked by `order` within the feature, lowest first. Without a validated plan
the feature never auto-promotes to review — flip it with `planning_set_feature_status`.

## planning_create_feature

Creates a feature on the board. Non-idempotent write; features cannot be deleted or archived via
MCP — the UI owns feature deletion.

**Slug generation.** Omit `slug` (or pass a blank one) and the Host generates
`{next free NNN}-{title-derived slug}`: lowercase ASCII derived from the title (Cyrillic
transliterated), non-alphanumerics collapsed to single dashes, the slug part shrinking so the
total never exceeds 64 characters while the number survives. An explicit slug always wins.

**Number collision.** The NNN prefix must be free — any feature holding that number (numeric
compare, so `7` and `007` are the same number) earns a 409 `NumberCollision` whose detail names
the colliding feature and suggests the next free number.

**Priorities.** p0 critical, p1 high, p2 normal (the default when omitted), p3 low.

**Next step.** `planning_upsert_artifact(kind=spec)`, then `overmind_notify(kind=approval,
featureSlug)` and the human's reply, then `planning_validate`.

## planning_set_feature_status

Moves a feature status on the user's explicit request in the current session only — never on the
agent's own initiative.

**Agent targets.** `done` (from any non-done status) and `inProgress` (review rollback). Every
other manual move belongs to the human door in the Overmind UI.

**The done gate.** The feature needs a result artifact first — without one the call fails with
409 `FeatureResultRequired`. It also needs a fresh human tap: an approval reply newer than the
result's last update. Without it the call fails with 409 `FeatureDoneTapRequired` — send
`overmind_notify(kind=approval, featureSlug=<slug>)`, wait for the reply, retry, or let the
human close the feature in the UI. `inProgress` needs no gate.

**Echo.** A successful agent-path `done` stamps the feature log with reviewed-by provenance,
echoed as `lastLogLine` unless a held claim adds its release line after it. `logNote` is
mandatory and must carry the provenance, e.g. "by user request in-session".

## planning_update_feature

Partial edit of a feature's `title`, `body`, `priority`, `lane` or `codeSurfaces`. Status is not
editable here — `planning_set_feature_status`. `logNote` is mandatory and lands as a dated
`[agent]` line in the feature log.

**Priority.** A priority change moves the feature to the tail of its new band.

**Lane.** `lane` assigns the feature to a lane roster entry by name. An unknown name
auto-creates the lane row (empty contour, no priority) — the assigning act is the birth, and
`planning_set_lanes` cannot create lanes. Empty string clears the lane, omit keeps it. A lane
whose last open feature leaves or closes retires itself.

**Code surfaces.** `codeSurfaces` declares repo-relative glob patterns this feature touches
(e.g. `src/web/**`). Only `*` and `**` wildcards are supported — `?`, `[`, `]`, `{`, `}` are
rejected. Advisory only: the queue, board and wave-snapshot rows project `potentialOverlaps`
between in-flight features so coordinators see cross-lane collisions. Empty array clears, omit
keeps.

## planning_patch_feature

Replaces or inserts one XML `<section>` of a feature body; the rest stays byte-identical. The
middle tier between a `logNote`-only note and the full-body `planning_update_feature`. The door
is claim-locked like every mutating feature door.

**Address mode.** Feature bodies carry the XML vocabulary only: `section` is a lowercase tag
name (letters, digits and hyphens, starting with a letter). There is no heading mode
(`'## Address'`), no acceptance-criteria tick, no brief and no amendment — those belong to
`planning_patch_artifact`.

**Insert.** A section absent from the body is inserted — with `content`, or declared-empty
(`'(none)'`) when `content` is omitted.

**Tombstone.** `remove=true` with `reason` replaces the resolved section, wrapper tags included,
with a single `<removed YYYY-MM-DD — reason>` line. `remove` without `reason` is refused, and
`content` must be omitted with `remove`.

**Guards.** The `<log>` section is append-only: patching it is a 400, and a logNote-only append
belongs to `planning_update_feature`. Replacing an existing section without content, an
unparseable section name, and a `remove` of an absent section are 400s naming the name and the
sections present. `logNote` lands as a dated `[agent]` feature-log line; the version bumps by
the same rule as `planning_update_feature` (draft keeps its version, any other status bumps it).

## planning_task_queue

The work order: non-done features sorted priority asc, then queue position; done features drop
out. Read-only. The board order IS the queue — this tool is its projection.

**Rows.** `slug`, `number`, `title`, `priority`, `status`, `queuePosition`, `specStatus`,
`planStatus`, `taskDone`, `taskTotal`, `taskCounts` (sums to `taskTotal`), `readiness` 0-3,
`blockedBy`, `isBlocked`, `createdAt`, `updatedAt`, and the claim block when claimed
(`claimedByAlias`, `claimedByVendor`, `claimedAt`, `claimState` alive | stale | released).

**Ceremony context per row.** `phase` — drafting | writing spec | planning | plan validated |
in progress | review | awaiting-human | done — derived server-side from spec/plan/task/result
state; `awaiting-human` means the work is complete and only the human's tap is missing.
`briefExcerpt` — the first `<goal>` paragraph capped at ~500 chars, null when the feature has no
goal.

**Materializer lag.** Every row carries `pendingMutations` (mutations enqueued but not yet
mirrored), `lastAttemptAt`, `lastSuccessAt` and `stale` (true while `pendingMutations > 0` or the
last attempt failed). A clean `BACKLOG/` tree with `stale=true` means the mirror is behind.

**Ordering and knobs.** `limit` truncates to the first N entries. The human reorders by dragging
cards within a priority band; the agent moves with `planning_move_feature`, and crossing a band
is `planning_set_feature_priority` with the user's explicit approval. `fields` / `compact` behave
as in `planning_list_features` and are mutually exclusive.

## planning_move_feature

Moves a feature within its priority band on the planning queue — band-scoped only; crossing
bands means `planning_set_feature_priority`.

**Moves.** `up` and `down` swap with the adjacent non-done feature and are no-ops at the band
edges; `first` and `last` jump to the band head or tail; `toPosition` takes the absolute 1-based
spot given in `position`. The server resolves neighbors and renumbers, so no band-wide read is
needed.

**Contract.** `position` belongs to `toPosition` alone and must be a positive int. An unknown
move, `position` on another gesture, or an out-of-range position returns error text naming the
contract; nothing moves.

## planning_claim_feature

Claims a feature for your adapter session. The claim is a hard mutation lock backed by session
liveness; reads stay open and the human door in the Overmind UI is never locked.

**Outcomes.** Feature done → 409. A live foreign session holding the claim → 409 naming the
holder, regardless of alias+vendor match — a name collision cannot hijack a live holder. A stale
or released claim by a different session → take-over succeeds and is logged. Your own alias+vendor
on an ended session (an adapter restart) → auto-adopt with a fresh session binding. Claims clear
themselves when the holder session ends or goes stale past the project threshold.

**Identity.** `alias` and `vendor` are optional (32 characters each) and fall back to the
identity the session registered with (`OVERMIND_ALIAS`/`OVERMIND_VENDOR`). A claim-locked write
that finds a stale or ended claim held by the caller's own alias+vendor adopts it and proceeds
instead of 409ing.

**Lane discipline** rides on the claim: a feature whose lane already holds an ALIVE claim by a
different alias+vendor pair is refused with 409 naming the holder. Unclaimed open features in the
lane and the caller's own claims never block, and the stale/ended take-over flow is unchanged.
`overrideLaneCheck=true` is the explicit escape hatch and is echoed back.

**Echo.** `slug`, the claim block, `lane` when set, `overrideLaneCheck` when it was used, and
`liveSurfaces` — globs plus owner aliases of running stands watched by the feature's lane that
overlap the feature's `codeSurfaces`, present only when something matches. Purely informational:
editing is never blocked.

## planning_unclaim_feature

Releases your claim on the feature. Holder session only — any other session gets a 409 naming
the holder; the human door in the UI can release any claim without session restrictions.

**No-ops.** Unclaiming an unclaimed feature succeeds without changing anything. A claim releases
itself when the holder closes the feature done.

## planning_set_feature_priority

Changes a feature priority (p0 critical, p1 high, p2 normal, p3 low); the feature re-enters at
the tail of the new band. Band crossings are the weightier act: only on the user's explicit
approval in the current session, never on the agent's own initiative. Moves within one band
belong to `planning_move_feature` or the human board drag.

## planning_pin_feature_code

Pins the explicit code location of a feature — a branch and/or a worktree path stored on the
feature itself — overriding the naming-convention discovery `planning_feature_code` performs
(branches and worktrees whose names carry the feature slug or NNN prefix). Use it when the code
lives on a differently named branch, several candidates match, or the worktree was created by
hand.

**Upsert semantics.** The body is a full replace, last write wins: resending one field keeps only
what you send. Both fields null or omitted clears the pin entirely, and clearing an empty pin is
a no-op success. Pinned locations are matched against the local listing first and flagged
`pinned`; a pinned location missing from disk is reported stale, never silently dropped. With no
pin set, `planning_feature_code` output is unchanged.

**Echo.** `slug`, `pinnedBranch`, `pinnedWorktreePath`, `lastLogLine`. Every actual change
appends one feature log line.

## planning_list_feature_dependencies

The dependency graph of one feature: `blockedBy` (features this one waits for) and `blocks`
(features waiting on this one). Each entry carries `slug`, `status` and `closed`; a dependency
closes when its blocker reaches done or review. Read-only. An unclosed `blockedBy` stops
`planning_current_task` from issuing tasks.

## planning_add_feature_dependency

Adds a blocker to a feature: it stops issuing tasks through `planning_current_task` until the
blocker closes (done or review). Self-reference and any edge that completes a dependency cycle
are rejected, naming the chain. Same-project only.

## planning_remove_feature_dependency

Removes one blocker from a feature. Removing a link that is not there is a no-op. Lifting the
last unclosed blocker unblocks the feature for `planning_current_task`.

## planning_read_artifact

One artifact by id or alias. The full body, or — with `sections` — the cheapest read on the
board. Read-only and idempotent.

**Addressing.** `artifactId`, or `alias`: `<slug>/T<order>` addresses a task by its order and
`<slug>/S`, `<slug>/P` and `<slug>/R` address the single spec, plan or result. The alias is
resolved against the feature roster, so it needs `projectId`, and it is mutually exclusive with
`artifactId`.

**Section reads are deterministic.** Sections are located on the canonicalized form (the same one
writes produce), found sections return in body order regardless of request order, and a name the
body carries never lands in `missingSections`. The full read stays byte-faithful to storage.
`missingSections` names requested-but-absent sections; a read never fails on a missing one.

**Payload.** `review` appears on task payloads only. Task payloads also carry `blockedBy` — the
sibling task orders this task waits on, `[]` when it has no edges; non-task kinds omit the field.

**Pointers.** Bodies may carry pointer markers: a case-sensitive `pointer:` as the first
non-whitespace token of a line, naming either a repo-relative path with an optional `#anchor` or
a grimoire crew-memory id (`grimoire://memory/<id>`). The server never reads the targets. Parsed
pointers ride the projections (`planning_current_task` payload, `planning_list_artifacts` roster
rows), not this read.

## planning_read_artifact_diff

Unified diff between two versions of one artifact — change hunks only, never full bodies.
Read-only.

**Version range.** `fromVersion` defaults to `toVersion - 1`, `toVersion` to the artifact's
current version; omitting both diffs the two most recent. Adjacent versions may carry log-only
bumps, so an empty diff between neighbors is a valid answer, not an error. A trailing newline is
ignored — `x` versus `x\n` diffs as empty, and unlike git there is no `\ No newline` marker.

**Errors.** An unknown `from`/`to` version fails with 404 `ArtifactVersionNotFound`; `from == to`
or `from > to` fails with 400 `InvalidVersionRange`.

**Addressing.** `artifactId`, or `alias` (`<slug>/T<order>`, `<slug>/S`, `<slug>/P`, `<slug>/R`)
with `projectId`; the two selectors are mutually exclusive.

## planning_upsert_artifact

Creates an artifact for a feature, or updates the existing artifact of the same `kind` and
`order` as a new version. The most expensive write: the full body is resent. Artifacts are
created `draft`; validation goes through `planning_validate`. Editing a validated artifact
requires `logNote` — creating one does not.

**Body shape.** Expected XML sections for a task: `goal`, `scope`, `anti-scope`, `constraints`,
`acceptance-criteria`, `refs`, `obstacles`, `log`. For spec, plan and result the body is
free-form markdown with `<log>` as the only machine-parsed section. `kind` is validated
(spec | plan | task | result) and `review` too (light | hard); unknown values fail with a named
error.

**Order.** A sort key within the feature, lowest first — it drives `planning_current_task`
picking. Omitted order appends after the current max (the first append lands on 0; materialized
backlog files are numbered 1-based). Gaps in order numbering are legal and meaningful: imported
backlogs skip tasks that were planned but never written. Order doubles as the stable handle —
`blockedBy` edges and `{featureSlug}/T{order}` aliases address tasks by order — so never
renumber existing tasks to close a hole.

**Task-only fields.** `brief` is a one-line summary of at most 500 characters shown next to the
task; other kinds reject it. `blockedBy` names sibling task orders this task waits on;
re-upsert replaces the edge set declaratively (empty array clears it), omitting it preserves the
current edges on update and sets none on create.

**dryRun.** Runs the full validation path — canonicalization, pointer lint, kind guards, order
resolution — and persists NOTHING: no row, no version snapshot, no feature transition, no
materialization dispatch. The response echoes the would-be artifact id and version marked
`dryRun`, so a huge body can be shape-checked before the real send.

**Idempotency.** `operationId` — the same GUID with an identical payload replays the stored
response with `replayed=true`; the same GUID with a different payload is a 409
`OperationIdConflict`; omit it to send no header (old Hosts ignore the header harmlessly).

**Guards.** Without a validated plan the feature never auto-promotes to review — flip it with
`planning_set_feature_status`. Features cannot be deleted or archived via MCP.

## planning_upsert_tasks

Creates or updates N task artifacts of a feature in one call — the batch counterpart of
`planning_upsert_artifact`, for decomposing a plan into tasks.

**Atomicity.** Items are staged and committed together; any failing item rolls back the whole
batch, so a retry starts clean. A failing item is reported with its index (`tasks[2]: …`).
Exactly one materialization pass is enqueued after the batch — it runs shortly after, in the
background, never per item — and one feature auto-transition evaluation runs; creating tasks
while the feature is in review reopens it once.

**Item fields.** `body` (required, non-blank), `order`, `review` (light | hard), `brief`
(task-only, one line, max 500 chars), `blockedBy` (sibling task orders this task waits on —
empty array clears, omitted preserves the current edges) and `logNote` under the same rules as
`planning_upsert_artifact`: optional, required only when the item edits an existing body that
demands one. An item whose `order` hits an existing task updates that task in place.

**Envelope lint.** `tasks` must be an array of objects with `body`; item fields never sit at the
call's top level. The adapter lints the envelope and the `pointer:` lines client-side, before any
HTTP call. A stringified array is accepted and re-parsed; anything else fails with the
accepted-form example.

**Echo.** `created`, `updated` and one compact row per item (`id`, `order`, `status`, `version`,
`bodyChars`, `brief` when set, `blockedBy`). Every response item echoes the resulting edge set as
`blockedBy` — `[]` when the item ends with no edges — so set, clear via `[]` and preserve via
omit are all observable in the reply. `dryRun` validates the whole batch and persists nothing,
returning would-be orders and ids marked `dryRun`.

**Idempotency.** `operationId` behaves as in `planning_upsert_artifact`.

## planning_set_status

Moves an artifact to a new status and records why in the artifact log. Planning ceremony step 3.

**Allowed statuses per kind.** spec/plan/result: draft, validated. task: todo, inProgress,
review, done, blocked. `validated` is the terminal for documents; result has no `done`.
Transitions are free-form — any allowed status of the kind can be reached from any current
status, there is no enforced sequence. `logNote` is mandatory on every move; an unexplained
status move is rejected.

**Log line.** Each move appends one line to the artifact's `<log>` section, shaped
`- YYYY-MM-DD [agent] — <logNote>`.

**Side effects.** A task closed done with unticked acceptance-criteria items still moves, and the
response carries a non-blocking warning counting them. Taking a task (inProgress, review or
done) also promotes its draft/ready feature to inProgress with an `[auto]` line. A spec or plan
moved to validated re-evaluates the review invariant — a feature whose tasks are all done lands
in review with exactly one `[auto]` promotion line.

**Human-proof rule.** Moving a spec or plan to validated carries the same proof requirement as
`planning_validate`: a live approval reply — a human answer to an
`overmind_notify(kind=approval, featureSlug=<slug>)` notification newer than the artifact's last
content update. Any body edit voids the reply, and without proof the move fails with a 409
`ValidationProofRequired`. Task and result moves never require proof, and the human UI door needs
no proof.

**Addressing.** `artifactId`, or `alias` (`<slug>/T<order>`, `<slug>/S`, `<slug>/P`, `<slug>/R`)
with `projectId`; mutually exclusive. **Idempotency.** `operationId` as in
`planning_upsert_artifact`.

## planning_append_log

Appends a dated line to an artifact's log without touching its body — the cheapest write: one
dated line, no body resent. Correct a line afterwards with `planning_edit_log`.

**Addressing.** `artifactId`, or `alias` (`<slug>/T<order>`, `<slug>/S`, `<slug>/P`, `<slug>/R`)
with `projectId`; mutually exclusive. **Idempotency.** `operationId` as in
`planning_upsert_artifact`.

## planning_edit_log

Corrects the text of the newest log line on one artifact — one line changes, the body is not
resent.

**Preconditions.** The target must be the artifact's newest `<log>` line and carry your own actor
marker (`[agent]` on this door, `[ui]` on the human door). Not-newest and wrong-actor attempts
are 409s naming the line's owner. `text` must be a single line: multiline replacements are
refused because the domain refuses splicing a second line into the log.

**Guarantees.** The date and actor marker are preserved byte-for-byte; only the prose after the
marker changes. The edit is version-traced — the previous body is snapshotted and recoverable via
`planning_read_artifact_diff`, nothing is deleted. Feature-level logs are out of scope
(append-only there): `planning_append_log` adds a line, this tool corrects the one already
written.

**Addressing.** `artifactId`, or `alias` with `projectId`. **Idempotency.** `operationId` as in
`planning_upsert_artifact`.

## planning_patch_artifact

Changes one artifact in place without resending its body — the middle write tier. Editing a
validated artifact requires `logNote`.

**One shape per call.** Exactly one of: a section patch (`section`, `content` optional), an
acceptance-criteria tick or un-tick (`acIndex` or exact `acText`), a task brief (`brief`), or an
amendment (`amend` with `content`). Never several, never none — a mixed or empty payload fails
with the patch-shape error naming the alternatives.

**Dual-mode section address, exact routing.** A tag-parseable name — the XML vocabulary `goal`,
`scope`, `anti-scope`, `constraints`, `acceptance-criteria`, `refs`, `obstacles`, `log` — always
routes to the XML section path byte-identically. A markdown heading address (`'## Scope'`) routes
to an H2 heading block of a spec/plan/result body. So a bare word like `scope` on a spec creates
or reaches the XML section, NOT the heading block — heading addresses need the `'## '` prefix.

**Section semantics.** A section absent from the body is INSERTED — with the content, or
declared-empty when `content` is omitted. Tasks accept the declared vocabulary only;
spec/plan/result accept any tag name. Replacing an existing section without content is a 400. An
unknown section name on a task is a 400 listing the sections present plus the accepted
vocabulary.

**Heading semantics.** A heading patch replaces from the heading line to the next same-or-higher
heading and keeps the heading line byte-stable; an absent heading with content inserts at the
body tail. A duplicate heading name is a 400 listing the matching headings, nothing modified.
Heading mode on a task is a 400 naming the XML vocabulary. A heading patch on a validated
artifact demotes it and needs `logNote` like any content edit.

**Tombstone.** `remove=true` with `reason` replaces the resolved heading block — heading line
included — with a single `<removed YYYY-MM-DD — reason>` line. `remove` without `reason` is
refused; removing an XML section is refused in v1 (`RemoveNotAllowedForXmlSection`) — tombstones
are heading-mode only.

**Acceptance-criteria ticks.** `acIndex` is 1-based and its numbering matches the materialized
mirror and the 400 diagnostics. `acText` is the recommended selector — fall back to the index
when the text is ambiguous. Text matching is entity-normalized: `acText '&lt;key&gt;'` matches a
stored `<key>` and the reverse, so escaped and readable forms are interchangeable on either
side. A miss never succeeds silently: an out-of-range `acIndex` or an unmatched `acText` is a 400
listing the visible items. `untick=true` clears the checkbox (`[x]` to `[ ]`). Flipping an item
already in the requested state is a successful no-op rendering "already ticked — no change" or
"already unticked — no change"; a replaced or inserted section is a real change every time. A
tick echoes the resolved item as `tickedIndex` and `tickedText`.

**Brief.** Task-only, one line, max 500 characters: no version bump, no log line, body untouched.
spec/plan/result reject it.

**The log section is append-only.** Patching `<log>` is a 400 pointing at `planning_append_log`
and `planning_edit_log`; a heading block carrying the `<log>` pair is refused the same way.
History is rewritten only through a full `planning_upsert_artifact`.

**The amendment door.** `amend=true` with `content`, on a `kind=result` artifact only. The
content is appended to the body tail as a dated `## <amendment> yyyy-MM-dd` block followed by the
content lines — one block per call, existing text untouched. A supersede note is just a line of
the content ("Supersedes: <what and why>"). NO demotion: a validated result stays validated, a
done feature stays done, and no human proof is required. `logNote` is mandatory and records the
pre-approval; when the append stales a live approval reply, the response's
`validationProofVoided` names it. Works on draft results too; on spec, plan or task the door is
refused with a 400 naming the result-only scope.

**Echo.** `id`, `version`, `status`, `lastLogLine`, `section`, `bodyChars`, plus the conditional
`tickedIndex`, `tickedText`, `statusChanged`, `featureStatusChanged`, `validationProofVoided` and
`replayed`.

**Addressing.** `artifactId`, or `alias` (`<slug>/T<order>`, `<slug>/S`, `<slug>/P`, `<slug>/R`)
with `projectId`; mutually exclusive. **Idempotency.** `operationId` as in
`planning_upsert_artifact`.

## planning_delete_artifact

Deletes one artifact as a soft delete: the body and version history stay in the database, the
materialized file is removed on the next pass, and an audit line lands in the FEATURE's log — the
feature log outlives the artifact, and `logNote` is the reason carried in that line.
Destructive; no restore and no force flag in v1.

**Guard matrix.** Agents can delete draft artifacts only. Deleting a validated or done artifact is
the human's door in the UI — a refused attempt comes back as a 409 naming the reason. A task with
incoming `blockedBy` edges is refused with 409 too ("clean up dependencies first"): deletion
never orphans dependents.

**Echo.** `id`, `kind`, `featureLogLine`. `source` marks who deleted it and defaults to `agent`
on this door.

**Addressing.** `artifactId`, or `alias` with `projectId`.

## planning_validate

Validates a spec, plan or result artifact. When the project requires human validation this fails
with a STOP error — ask the user to approve it in the Overmind UI instead of retrying.

**Proof recipe (spec and plan).** Send `overmind_notify(kind=approval, featureSlug=<slug>)`, wait
for the human's reply in the notification center, then validate: notify → reply → validate. The
reply must be newer than the artifact's last update; every body edit voids it, and
re-validation needs a fresh reply. Without proof the call fails with a 409
`ValidationProofRequired`. One fresh reply may validate the spec and the plan of the same feature
in one pass — and any body edit to either voids it for both.

**Human door.** The UI needs no proof, and neither does `asMigrated`.

**asMigrated.** The migration escape hatch for artifacts pre-approved before import (e.g.
already validated on disk): it skips the human gate, requires a `logNote` carrying the
provenance, and leaves a "validated as migrated — pre-approved on disk" marker line in the
artifact log.

## planning_import_feature

Imports one materialized feature folder from disk in a single call — bodies never pass through
the model.

**What is picked up.** Give the absolute path of the folder that holds `feature.md`, under the
project root (e.g. `/repo/BACKLOG/058-agent-side-import`). `spec.md`, `plan.md`, `result.md` and
`tasks/NN-*.md` are picked up by convention, statuses are restored verbatim with `[import]`
markers in the logs, and existing artifacts are re-imported as new versions.

**Rules.** Relative paths and paths outside the project root are rejected. Import never enforces
the create-path number-collision guard — a mirror with colliding NNN numbers restores verbatim.

**Echo.** Created vs updated counts per feature and artifact, plus per-file notes.

## planning_materialize

Triggers a materialization pass inline and waits for the report.

**Atomicity.** The full plan is computed first, then written atomically. Any guarded file —
untracked at a planned path, or tracked with local edits not on the board — aborts the whole run
with a 409 listing every refusal, and nothing is written that run. Skips in the report are
byte-identical no-ops (content already matches the board), not guarded files.

**When to call.** A normal board mutation already enqueues a pass that runs shortly after in the
background. Call this after an import, a strategy switch, or when `overmind_onboard` reports a
failed attempt or stale mirror state.

**force.** Adopts guarded files with per-file warnings instead of refusing them. Transcribe hand
edits to the board before forcing.

**Echo.** `projectId`, `strategy`, `written`, `skipped` (count) and `skippedHead` (the first five
as `path: reason`), `force`, `lastSuccess`, `lastAttempt`.

## planning_materialize_status

Mirror diagnostics without filesystem access — read-only, it never triggers a pass;
`planning_materialize` does.

**Payload.** `lastSuccess` versus `lastAttempt` (`lastAttemptReason` carries the failure when the
attempt did not succeed), the persisted last-run report per file (`startedAt`, `finishedAt`,
`atomic`, written paths, skipped and refused with reasons), `trackedFiles`, `stalePaths` (tracked
files the last run neither wrote nor skipped, plus tracked files whose disk bytes drifted),
`strategy` and `targetDir`. `lastRun` is null when the failed attempt persisted no report — read
that as no report for this attempt.

## planning_export_backlog

Renders the full materialized mirror (planning tree plus the entries projection) into an explicit
target directory as plain greppable files — the "commit the planning alongside the code" path.

**Fidelity.** Uses the same document renderers as the materializer pass, so the snapshot is
byte-identical to what a materialize run would write; zero tracking rows and zero drift machinery
are created.

**Target rules.** `targetPath` is always explicit — there is no default. The directory must be
empty or not exist yet; a non-empty target refuses with 409 (this is a snapshot, not a sync), and
the path must not sit inside `BACKLOG/` or `ENTRIES/`.

**Echo.** `projectId`, `target` and the written file `files` count — no body echo.

## planning_batch

Applies several ceremony ops to ONE feature atomically in a single call — one turn per ceremony
step instead of one turn per write.

**Atomicity.** Ops apply in request order under one transaction. Any failing op rolls the whole
batch back (nothing persists) and its 400 names the index (`ops[1]: …`). Door-level op errors
keep the batch atomic; post-commit notification and materialization failures do NOT roll anything
back — they ride the response as `warnings[]`.

**Vocabulary** (snake_case op names): `patch` carries the patch payload of its standalone tool
(`{op: tick_ac | untick_ac | replace_section | set_brief | amend_result | remove_section}` plus
that op's fields, same shapes as `planning_patch_artifact`); `append_log` carries `note`;
`set_status` carries `status` plus `logNote`; `upsert_tasks` carries `tasks[]` with the same item
shape as `planning_upsert_tasks`; `notify` carries `kind` and `text` plus optional `tab`,
`featureSlug` and `from`, where `featureSlug` defaults to the batch feature.

**Addressing.** `patch`, `append_log` and `set_status` address the artifact by `artifactId` or
`alias` — `<slug>/T<order>`, or the bare `T<order>`, `S`, `P`, `R` forms, since the batch is
bound to one feature by the route — and cannot name artifacts created by earlier ops of the same
batch (v1).

**Envelope lint.** The adapter checks the envelope before any HTTP call: `ops` must be a
non-empty array of objects each carrying `op` plus that op's fields; an unknown op or a missing
required param fails locally with no round-trip.

**Echo.** `featureSlug` plus one compact row per op in request order — echo fields only, never
artifact bodies. **Idempotency.** `operationId` as in `planning_upsert_artifact`.

## planning_rescue_scan

Which dead sessions left work nobody pushed. Read-only and idempotent: the board half comes from
the host, the git half is measured adapter-side — the host never runs git.

**Inputs.** `slug` optional (scan that candidate only), `projectId` optional and resolved from the
working directory as everywhere else.

**Board half.** `GET /api/agent/projects/{projectId}/planning/rescue-candidates`. Every host row is
echoed verbatim — `slug`, `title`, `status`, `claimState`, `claimedByAlias`, `claimedByVendor`,
`claimedAt`, `claimedByHuman`, `pinnedBranch`, `pinnedWorktreePath`, `updatedAt` — this tool only
adds fields and renames none. `generatedAt` is the host stamp, copied to the top level.

**Vocabulary.** `claimState` is RAW liveness and only ever `stale` | `ended`. The board projection
and `overmind_wave_snapshot` report the same ended holder as `released`; the rescue door and this
scan do not, on purpose — the rescue question is whether anybody still holds the work, and an
ended holder answers it differently from a claim released on purpose. A consumer must not
normalize one side into the other.

**Deleted holders, and why the two surfaces differ.** A claim whose holder's `AgentSessions` row is
gone from the registry reads as `ended` here — the rescue door treats a missing row exactly like an
ended one, and `planning_claim_feature` does the same, so the work stays visible and taking it over
stays allowed. The `claimChurn` block of `overmind_onboard` deliberately does not list such a holder:
every churn column is heartbeat-derived (`unseenSince` is the end stamp or the last seen, and
`deathAgeMinutes` is computed from it), and a deleted row has no heartbeat left to report. Inventing
one from `feature.updatedAt` would print an unmeasured number as measured silence, so churn stays a
join over the session rows it can actually date. Nothing in the product deletes session rows — this
state arrives from direct database surgery or a future janitor — which is why the rescue door is the
surface that owns it. An agent that finds an orphan here and not in churn has seen the whole answer,
not a gap.

**Policy block.** `policy.text` is the stored `RescuePolicy`, `policy.autonomy` is `owner-set`
while the text is non-blank, `not-set` after the owner cleared it, `unreadable` when claim-settings
did not answer; `policy.guidance` says what a rescuer may do. Blank means NO autonomy: a cleared
policy is an owner directive, not a missing default, and neither this tool nor the migration
re-seeds the default text over a deliberate clearing — the migration that seeded the default touched
only rows that were already null before it ran. With `not-set` or `unreadable`, report the facts and
ask the owner before pushing or merging.

**`rescueAllowed` is a policy answer, not an evidence answer.** The per-row flag reports what the
owner's standing directive permits, so under an `owner-set` policy it stays `true` even on a row whose
`verdict` is `unknown` — a hung git or unmeasurable signals remove the proof, not the permission.
Forcing `false` there would fuse the two axes and could block a rescue the owner explicitly allowed
("everything unpushed may be pushed") just because git hung on one row. The one exception is a
listing failure: with no evidence anywhere every row is forced `false`, and that is a capability
statement, not an evidence one — such a row carries `code: null`, so there is no location a rescue
could act on whatever the policy says. Consumers must read `verdict` and `reasons` before acting on
the flag; an allowed-but-unknown row is permission without proof, and the gaps are named in `reasons`
precisely so the rescuer can decide instead of guessing.

**Scan root.** Git runs in the project's `diskPath` when that directory exists and sits inside a
repository, otherwise in the adapter's working directory; `scanRoot.source` names the choice
(`project-disk-path` | `adapter-workspace`) and `scanRoot.adapterWorkspace` carries the cwd either
way.

**Where the code is** (`code`): `pinnedWorktreePath` and `pinnedBranch` win (`source: "pin"`); with
no pins the adapter falls back to the `feat/NNN-slug` convention over `git worktree list
--porcelain` plus `for-each-ref refs/heads` — the same rule `planning_feature_code` answers with
(`source: "discovery"`); nothing matched → `source: "none"`. `worktreeExists` and `branchExists`
are measured, never inferred from the pin; `stale` marks a worktree path that the pin or the
convention named and the disk no longer has.

**Git facts** (`git`; each field is null when it could not be measured):

| Field | Command | Reading |
| --- | --- | --- |
| `dirtyFiles`, `untrackedFiles` | `git status --porcelain` | counted in the worktree, so a gone worktree carries no dirt signal at all |
| `unpushedCommits`, `unpushedBase` | `git rev-list --count <base>..HEAD` | `<base>` is the branch's upstream; a branch with no upstream falls back to `origin/main` |
| `unmerged`, `mergedInto` | `git merge-base --is-ancestor HEAD <mainline>` | exit 0 merged, exit 1 not merged, any other exit unknown; `<mainline>` is the first of `origin/main`, `main`, `master` that exists locally |
| `lastCommit` | `git log -1 --format=%h%x09%cI` | `{hash, date}` |

**Verdict per row.** `rescue-worthy` when anything is uncommitted, ahead of its base or unmerged —
`reasons` names exactly which fact. `clean` needs at least one *measured* signal among
`dirtyFiles`, `unpushedCommits`, `unmerged` to have said so, and it lists the halves that stayed
unmeasurable; the "branch is merged and pushed" line is spoken only when both halves were measured
clean — absence of data never claims safety. `stale` when the worktree is gone and no branch
survived to hold the work; `unknown` when git could not answer, including a row where only
`lastCommit` came back, and a row whose per-row git call threw (the scan survives, the other rows
keep their facts). `counts` groups verdicts across the rows.

**Reasons mirror the fields.** The `worktree path … is gone, only the branch is left` line is spoken
by every verdict that still knows where the work is — `rescue-worthy`, `clean` with an unmeasured
push half, and `unknown` — so the prose cannot disagree with `code.stale` and
`code.worktreeExists`. The stronger "gone, but the branch is merged and pushed" replaces it only when
both halves were measured clean, and a row where nothing survived on disk says only
`no branch or worktree` — never "the branch is left", because it isn't. Cancellation is not a
degraded answer either: a cancelled caller token propagates out of the tool rather than being
recorded as a per-row git failure, so a scan the caller pulled mid-flight never reports the work as
merely unknown.

**Degraded answers.** A top-level `discoveryError` means git answered nothing — the scan root is not
inside a repository, or a listing command failed — and the rows still carry the board answer with
`verdict: "unknown"`: the `planning_feature_code` `discoveryError` contract repeated here, not a new
one. A missing worktree is a `stale`-marked row, not an error. Per-row `git.error` carries what the
commands reported, including a ref name too unsafe to query.

**What it never does.** Nothing is claimed, pushed, merged or written: the rescue stays the PR flow
plus the review invariant, and the take-over itself is `planning_claim_feature`. Companions:
`overmind_wave_snapshot` for the live wave picture, `planning_feature_code` for where one feature's
code is.
<!-- region:planning:end -->
<!-- region:others:start -->

The entries below are diet batch B: every non-planning, non-wake tool. One shared rule for all
of them: where the parameter list carries an optional `projectId`, it resolves from
`OVERMIND_PROJECT_ID` or the calling adapter's working directory when omitted; reads and
mutations never create a project row unless the section says so, and an unregistered directory
returns guidance instead of an error.

## list_projects

No additional prose — the tool description lost nothing.

## list_entries

- **Metadata rows** (default projection): `id`, `type`, `title`, `tags`, `createdAt`,
  `updatedAt`, `contentLength` (content length in characters). Bodies arrive only with
  `include_content=true` or per-entry via `read_entry`.
- **`state` default**: when omitted, the server applies its own default — `active` for the
  decision/gate/guard trio, `all` for every other type and when no type filter is given.
  A lifted rule is a rule that no longer applies; `superseded` rows carry a successor.
- **`fields` grammar**: comma/space-separated metadata keys; each row collapses to
  `{id, ...requested}` in request order; `id` always leads and stays; unknown keys are
  rejected before the call naming the allowed set; a list naming no keys is a lint error;
  `include_content=true` adds `content` to the allowed set.
- `q` fulltext narrows the listing and is capped at 100 hits; all matching entries return in
  one array, most recently updated first — there is no pagination.
- An unknown `type` value fails naming the expected set (note, decision, gate, guard, skill,
  idea, audit, tombstone).

## read_entry

No additional prose — the tool description lost nothing.

## write_entry

- Creates a new entry in a project; `projectId` is required in the schema sense but optional
  per call (resolution rule above); resolution here never auto-creates a project row.
- A hand-made `tombstone` buries an approach without deleting anything.
- `idea` is the informal lane for unformed product thinking — no statuses, no lifecycle;
  promotion to a board feature stays a ceremony act.
- `enforces` is decision-only: it marks the decision as a standing rule agents must obey;
  the server refuses `enforces=true` on any other type.
- Skill entries are refused (`SkillManagedElsewhere`) — managed via the Overmind UI.
- Returns a compact echo: `id`, `type`, `version`, `updatedAt`, `bodyChars`; decisions also
  carry a `hint` line suggesting the grimoire mirror plus
  `pointer: grimoire://memory/<id>` linkage.

## update_entry

- Replaces content only — title, tags and type are kept; the entry type is read back first
  and skill entries are refused.
- `enforces`: an explicit value sets the decision-only standing-rule flag (the server refuses
  `true` on non-decisions); omitting it keeps the current value.

## delete_entry

- Deletion is permanent: the row and its version history are gone. A tombstone entry is born
  in the place carrying the original title with `' (deleted)'` appended, the original type,
  the deletion date, door + actor provenance, the optional `reason`, and the final content
  preserved in a fenced block.
- Deleting a tombstone is a plain delete — no second tombstone.
- Tombstones stay discoverable: `list_entries` with `type=tombstone`; `search` rides them
  like every other entry.
- Kit-linked entries are refused (`KitOwnedEntry`: managed by a skill kit — the kit reimport
  or the UI owns it); skill entries are refused too.
- Unlike other entry doors, project resolution here creates the project row when missing.
- Returns `{id, type, deleted: true}`.

## lift_entry

- Applies to a standing rule (guard, gate, decision) that no longer applies. State moves to
  `lifted`, or to `superseded` when `supersededById` points at the replacement entry; the
  reason, optional proof, actor and date are stamped on the entry as provenance
  (`stateReason`, `stateAt`).
- The body, title and tags stay untouched; re-lift is allowed, last write wins.
- A lifted entry leaves the active default listings (`list_entries` trio default, briefing
  trio sections) and surfaces in the briefing's `liftedRecently` tail.
- Lift refuses skills (the UI owns the skill lifecycle) and tombstones (already the ceiling).
- The supersede target is validated: unknown, cross-project, tombstone or self-reference
  targets come back as readable errors.
- Returns the lifecycle echo (`id`, `type`, `version`, `state`, `updatedAt`, `bodyChars`,
  `supersededById` when set).

## append_entry

- One dated `## Follow-up yyyy-MM-dd` block lands at the content tail through the version
  machinery: the previous content is snapshotted (the version bumps) and stays readable via
  the versions endpoint; the search index refreshes.
- `text` is capped at 2000 chars enforced adapter-side before the call; blank text is
  refused; skills are refused.

## search

- Fulltext covers entries plus planning features and their spec/plan/task/result bodies.
- Semantic mode searches entries only and fails with `vector search unavailable` when the
  vector store is down.
- `since`/`until` bound hits by createdAt (`since` inclusive, `until` exclusive) and `author`
  matches the exact alias case-insensitively — setting any of them drops feature and artifact
  hits (entry-only result set).
- Each hit carries its `source` (`entry`, `feature` or `artifact`) with only its id fields, a
  title or label, and a snippet — a short window of lines around the first query-term match.
  Full bodies are not returned: `read_entry` or `planning_read_artifact` for depth.

## read_project_manifest

- Returns the manifest markdown verbatim. When no manifest is set the tool says so instead of
  failing — set one on the project config page in the Overmind UI or import it from
  `AGENTS.md`/`CLAUDE.md`/`QWEN.md`.

## planning_set_lanes

- Upsert semantics: lanes are keyed by name, case-insensitively; only lanes the payload names
  change; lanes absent from the payload are never touched, so a payload missing a lane
  deletes nothing. An empty array is an explicit no-op echoing the unchanged roster — it used
  to clear the whole roster and no longer does.
- An unknown lane name fails with `400 LaneBirthRestricted`: this tool never creates lanes —
  the birth doors are `planning_create_lane` and assignment via `planning_update_feature`'s
  lane field (which auto-creates unknown names). Removal is `planning_remove_lane`.
- Lane item fields:
  - `name` — required, max 64, unique per project, addressable as a single URL path segment:
    no slash, backslash or control character, never a bare dot or dot-dot — those are refused
    with `400 InvalidLaneName` before anything is written.
  - `contour` — free text description of the code contour.
  - `serialOrder` — optional int, reserved; no behavior yet.
  - `priority` — optional int 0-3, mirroring feature priorities p0-p3; lane columns sort
    priority asc, nulls last; null = no priority.
  - `boundAlias` — optional, max 32: binds the lane to one agent alias so the resident
    re-adopts it after a restart via `session_identity_set`; vendor is intentionally not
    bound (it is per-runtime); null or omitted clears the binding — item-level replace, like
    every optional field.
  - `liveSurfaces` — optional array of `{glob, owner}`: repo-relative globs watched by a
    running stand, owner = the alias running it, glob flavor identical to `codeSurfaces`;
    entries whose globs match a feature's `codeSurfaces` appear in that feature's claim
    response as an informational warning, never a block. Omitted clears the lane's entries —
    item-level replace.
- Max 64 lanes per project. Returns the resulting roster with per-lane holder and
  open-feature counts.

## planning_create_lane

- The create door `planning_set_lanes` refuses. `name` rules and the `InvalidLaneName`
  grammar are shared with `planning_set_lanes` (max 64, URL-path-segment safe, trimmed before
  the write; the echo carries the stored spelling).
- Birth is idempotent by echo: a name already on the roster (case-insensitive) is not an
  error — the stored row comes back unchanged and nothing is written, so a retry after a
  dropped connection is safe. A concurrent race that slips past the dedupe surfaces as
  `409 LaneNameDuplicate`.
- The roster caps at 64 lanes per project — over the cap fails with `LaneRosterTooLarge`.
- No claim lock is involved: lanes are roster rows, not board artifacts; creating one never
  blocks on or touches a feature claim, and moves no features. Features join via
  `planning_update_feature`'s lane field.
- `boundAlias` omitted leaves the lane free for adoption via `session_identity_set`.
- Returns a one-line echo of the stored lane.

## planning_remove_lane

- The only manual removal path: `planning_set_lanes` never deletes, and a lane whose last open
  feature leaves or closes retires itself.
- Refuses with `409 LaneStillOccupied` while the lane still holds open features, naming their
  slugs — close them as done or move them to another lane first.
- A delete on an unknown name is an idempotent no-op.
- Removing a lane touches no features: the lane field is a plain name reference, never an FK.
- Returns the resulting roster with per-lane holder and open-feature counts.

## planning_list_lanes

- Per-lane fields: `name`; `contour`; `serialOrder` (reserved, no behavior yet); `priority`
  (0-3, null = no priority — the board sorts lanes priority asc, nulls last); `boundAlias`
  (null when free — entering a lane means adopting it via `session_identity_set`, or binding
  your own before claiming); `liveSurfaces` (`{glob, owner}` entries of running stands the
  lane watches, shown verbatim; entries whose glob matches a claimant feature's
  `codeSurfaces` surface as informational warnings in `planning_claim_feature` responses);
  `openFeatureCount` (non-done features currently in the lane); `holder` (alias + vendor +
  claimState of the alive claim on any feature in the lane); and the features themselves
  (slug, status, claim identity, claimState).
- The wave marshal reads this before deciding who moves — a lane is busy when some feature in
  it holds an alive claim by another agent.

## issues_create

- Files an issue (or, with `kind=request`, a feature-request card) on the target project's
  board. The owner team triages and promotes.
- `kind` accepts exactly `'issue'` (default) or `'request'` — both carry `authorProjectId` as
  provenance (validated against existing projects, not permission) and both promote into
  tasks the same way; anything else fails with a named validation error.
- Body is free-form markdown; recommended sections `'## Repro'`, `'## Expected'`,
  `'## Actual'`. A blank body is refused.
- A foreign create (`authorProjectId` differs from the board) posts a notification-center
  entry on the owner project with a deep link — the owner team hears about it without
  polling.
- Follow-ups belong on `issues_append`; never file a duplicate issue for new information.
- Returns `{id, status, title, kind}`.

## issues_list

- Metadata rows only (no body, no append texts): `id`, `title`, `status`
  (open/triaged/promoted/closed), `authorProjectId`, `createdAt`, `closedAt`,
  `promotedToArtifactId`, `appendCount`, `kind`.
- Composable filters: `status`, `authorProjectId`, `kind` (issue | request). Reads are open to
  any project — provenance, not permission.

## issues_get

- Full card — title, body, status, author provenance, lifecycle timestamps — plus the
  chronological append trail. Reads are open to any project.

## issues_append

- Follow-up entries land in the append-only trail as `authorProjectId + text + createdAt` and
  are never edited or deleted. Works from any project.
- The trail moves into the task's `<obstacles>` when the issue is promoted.
- Returns the appended row (`id`, `authorProjectId`, `text`, `createdAt`).

## issues_set_status

- Lifecycle: open → triaged → promoted → closed. Settable here: `triaged` and `closed`;
  `promoted` is refused and reached only through `issues_promote`.
- Closed is reachable from any non-closed state (the not-a-bug close — put the reason in
  `issues_append` text). There is no reopen: a re-found bug is a new issue.
- Every move records the `source` marker (Agent or Ui) as provenance — issues have no log.
- `logNote` is accepted for protocol symmetry only and is unused.
- Returns `{id, status, source, closedAt}`.

## issues_promote

- Composes a task artifact on `featureSlug`: goal from title + body, scope from the
  Repro/Expected/Actual sections, the append trail into `<obstacles>` verbatim, a `<refs>`
  entry pointing at the issue; the issue moves to promoted with a link to the artifact —
  traceable both ways.
- Only from open/triaged; promotion belongs to the board's team. A claimed target feature
  blocks promotion from any session that does not hold the claim — in any liveness state
  (promote is not take-over): resolve the claim with the holder or wait for the owner team.
- Returns `{id, status, artifactId, featureId, artifactVersion}`.

## wave_chat_post

- Signal-only by etiquette; an `@alias` mention means the addressee must read this. Text is
  trimmed and capped at 500 characters, enforced adapter-side; the author is the calling
  session's registered alias.
- An unknown room fails with the named `MissingWaveRoom` error; overlong text with
  `WaveTextTooLong`.
- Room names are URL-escaped as a single path segment on the wire (main PR #78 / commit
  097a09f): rooms whose names carry slashes or other reserved characters stay addressable.
- Returns one line: message id, room, author alias and the mention list when any.

## wave_chat_unread

- Each room read delivers messages newer than the alias's watermark; a message mentioning the
  alias is delivered once by that same read — the mention clause is scoped by the watermark,
  so nothing re-delivers on later reads.
- Without `rooms` the tool reads every room that has activity, oldest last-activity first, so
  one room's watermark cannot blind a quieter room. `rooms` is comma-separated (lane names
  and/or `'all'`); a list of blanks is refused.
- The merged feed is deduplicated by message id and ordered by `createdAt`. Echoes
  `projectId`, the selected `rooms` and `watermarkAt` (null when nothing was delivered).
- `since` (ISO datetime) narrows the effective lower bound to `max(watermark, since)` and
  never widens delivery. `limit` caps the page to the NEWEST window — the mention slice
  outside it needs `replayMentions=true`. `replayMentions` re-delivers every message that
  ever mentioned the alias, without the watermark bound; it is not older traffic in general.
- `compact` projects each message to `{id, room, authorAlias, createdAt, text}` with text
  truncated to ~200 characters.
- An unknown filtered room fails with the named `MissingWaveRoom` error. Room names are
  URL-escaped as a single path segment (see `wave_chat_post`).

## wave_chat_peek

- One `{room, count, ids}` entry per room: `count` is the number of undelivered messages,
  `ids` lists their message ids — read full bodies with `wave_chat_unread` only when
  count > 0. No watermark advances; reads are pure.
- Same selection as an unread read: room traffic above the watermark plus watermark-scoped
  mentions. Mentions (`@alias`) surface in the entry of every room they were posted to: ids
  can repeat across rooms, so summing the counts overstates what a single `wave_chat_unread`
  read delivers.
- `rooms` omitted reads every room with activity, oldest last-activity first; an unknown
  filtered room fails with `MissingWaveRoom`. Room names are URL-escaped as a single path
  segment (see `wave_chat_post`).

## overmind_wave_snapshot

- **Host half** returns: the task queue (`planning_task_queue` rows verbatim), per-claim
  identity (alias, vendor, claimState, claimedAt, claimedByHuman, last board activity), per
  in-flight feature (claimed or inProgress/review) code pins, open feature blockers rolled up
  per feature, and the latest coordination entries by tag (default `wave-sync`,
  `lane-marshalling`).
- **Adapter half** enriches in-flight features with repo code discovery (the
  `planning_feature_code` projection: worktrees/branches carrying the slug or NNN prefix,
  pins matched first and flagged, stale pins reported). The git listing is cached briefly —
  repeated snapshots in one marshal pass do not spawn git processes per request. A cwd
  outside any git repository is an expected condition reported as the short
  `discoverySkipped` marker; a genuine discovery failure degrades to `discoveryError` and the
  rest of the payload is unaffected.
- **`potentialOverlaps`** on each queue row is advisory, never a block: overlap rows report
  glob collisions between in-flight features so a marshal or a human can judge them. Each
  match pairs one side's glob with the partner's (`pattern`, `partnerPattern`) and labels the
  kind: `exact` = the two globs are literally equal strings (both features declare the same
  file or directory); `sharedDirectory` = the patterns only share a boundary directory prefix
  and may be completely disjoint — a disjoint-file-set verdict stays the marshal's or the
  human's call.
- **`compact=true`** projects every section to coordination-critical fields only and stays
  sparse: null values, empty arrays and default scalars (readiness 0, stale false, isBlocked
  false, openDirectiveCount 0) disappear; queue rows drop briefExcerpt, taskCounts, spec/plan
  statuses and materializer stamps, keep task progress only when taskTotal > 0, claim fields
  only when claimedByAlias is set and lane fields only when lane is set; claims drop
  lastActivityAt and claimedByHuman=false; entries drop snippets and empty tags; discovery
  rows drop the per-row workspace (the top-level workspace stays); code pins and blockers pass
  through unchanged; queue rows keep potentialOverlaps verbatim (withFeature, sharedGlobs,
  matches).
- **`fields=<keys>`** instead projects queue rows and claims to `{slug, ...requested}` —
  queue rows speak the `planning_task_queue` grammar and claims add claimedByHuman and
  lastActivityAt — while every other section passes through unchanged. Unknown keys are
  rejected naming the allowed set; a list naming no keys is a lint error. `compact` and
  `fields` are mutually exclusive; omitting both returns the full payload.
- `projectId` is required — the tool errors without it; resolution never creates a project
  row.

## overmind_notify

- Posts a notification to the center the human watches in the Overmind web UI. `kind` is
  info, question or approval (anything else is a named validation error); `text` max 2000
  chars.
- `featureSlug` and `tab` (spec, plan, tasks or result) make it a deep link onto the feature
  page; the response may carry `url` (set when `featureSlug` is) — quote the url in chat when
  asking for an approval or reporting a question so the human can click through.
- `from` is the sender name shown next to the notification; defaults to `'agent'`.
- Project resolution here creates the project row when missing (unlike the read doors).
- Use question or approval when a gate needs the human's call, then pick the answer up with
  `overmind_replies`.

## overmind_replies

- Notifications you sent via `overmind_notify` that carry a reply, newest first; each item's
  `replyText` is the human's decision. Poll it after asking a question or requesting
  approval.
- Filters combine (AND): `since` — only replies newer than it, e.g. the artifact's last
  update when hunting a validation proof; the zone offset is optional and offset-less values
  read as UTC, the way artifact timestamps are emitted. `kind` — info/question/approval.
  `featureSlug`. An over-narrow filter returns an empty array, not an error.
- Delivery marks the notification read (`includeRead` semantics: false matches nothing —
  poll with the default).

## overmind_onboard

- Resolves the project from the adapter's working directory, creating it if needed — the one
  door that creates a project row. Set `OVERMIND_PROJECT_ID` to a valid project guid to skip
  cwd matching — use it for symlinked or worktree layouts where the resolved cwd differs from
  the registered project path.
- The default payload's blocks beyond project id, board summary and quickstart:
  - **materialization** — strategy, targetDir, trigger, lastSuccess vs lastAttempt (the
    failure reason rides along when the attempt did not succeed), the skipped-file count with
    the head of skipped paths from the last successful pass.
  - **validation** — humanGate plus the state-truthful gate sentence: off = agent validate
    requires a live approval proof (`overmind_notify(kind=approval)` with a human reply newer
    than the artifact's last update; `409 ValidationProofRequired` without it); on = agents
    STOP until a human validates the artifact in the UI.
  - **agentIdentity** — the effective pair and where it was read from: the Host answers with your
    live session row (`X-Overmind-Session`), the adapter names the source that actually produced it
    — `slot` (set this session via `session_identity_set`), `env` (`OVERMIND_ALIAS`/`OVERMIND_VENDOR`
    agree with the row), `session` (the row carries a real pair the adapter's own environment does
    not — the restart restore wrote it, or the host renamed the session), `placeholder` (nothing
    anywhere). Plus the project's naming rules, the `session_identity_set` next step when the alias
    is still the shared placeholder, and the "already in force, do not rename" line when it is not.
    With no naming rules set anywhere, the `namingRules` text depends on that pair: a placeholder
    pair gets the "invent an alias" hint, a real pair in force gets the hint saying this pair
    already satisfies naming and should be kept — so the hint and the in-force line never give
    opposite advice.
  - **claimChurn** — absent when nothing is orphaned; otherwise the holders that stopped answering
    while still holding claims in this project: pair, `ended` or `stale`, death age, and the slugs
    (capped at 10 per holder with the total alongside, 5 holders, freshest death first).
    `sessionsTotal` is the churned-holder count before the 5-row cap, and the renderer turns the
    difference into a `+N more holders not listed, M churned in total` line, so a holder the cap
    dropped is accounted for instead of vanishing. A pair that
    is yours adopts on your next write; a foreign one takes `planning_claim_feature`. A holder whose
    session row was deleted from the registry is not listed here at all — the reconciliation note
    under `planning_rescue_scan` says where it is.
  - **semantic** — search availability, with the verbatim probe reason when down.
  - **adapter** — adapterVersion vs hostVersion, with a restart warning when they diverge or
    the adapter predates version reporting.
- `brief=true` returns only the project id, board counts per status, humanGate and the
  adapter version — the cheap repeat call for agents already onboarded and working a lane. It
  deliberately carries neither agentIdentity nor claimChurn: the identity read is a start-of-work
  act, and the churn list is a take-over guard, both priced above a repeat poll.

## planning_append_directive

- The one write that crosses the claim lock: any session may append to ANY feature regardless
  of claim ownership or feature status — a HOLD / SYNC / YIELD from a wave marshal or lane
  neighbor reaches a live holder without taking over the claim.
- Directives are append-only and attributed: `text` is trimmed, capped at 2000 chars enforced
  adapter-side, and stamped with the sender's session identity. Nothing else is touched — no
  artifacts, no statuses, no version bumps; the 409 claim lock itself stays intact.
- The holder sees open directives on `planning_current_task` and `planning_read_feature`
  payloads (`openDirectives`, oldest first) and on queue rows as `openDirectiveCount`; acks
  via `planning_ack_directive`.
- Etiquette example: `HOLD: proxy.ts is shared with 134 — sync before the next push`.
- Echo: directive id + the holder-sees/acks reminder.

## planning_ack_directive

- The holder (or any session) marks a directive as seen; the ack is attributed to the calling
  session. Acked directives leave the `openDirectives` projections and the queue
  `openDirectiveCount` drops.
- Append-only channel: an ack flips only the ack stamp — never the directive text, the
  artifacts, statuses or versions. Acking an already-acked directive is an idempotent no-op
  success ("already acknowledged — no change").
- `directiveId` comes from `openDirectives` on `planning_current_task` or
  `planning_read_feature`.

## overmind_briefing

- Sections: guards and gates with full bodies; decisions and the latest notes as ~200-char
  snippets (`read_entry` for depth); tombstones of buried entries; `liftedRecently` tail —
  entries lifted or superseded within the last 14 days, max 5 rows, newest first: title,
  state, date, reason snippet, successor title for superseded.
- Guards, gates and decisions render active entries only; decisions carrying `enforces=true`
  additionally appear in the guards section marked `decision·enforces`.
- When the lane roster carries `liveSurfaces` (globs + owners of running stands watched by a
  lane), a `liveSurfaces` section lists them so the start-of-work picture warns before any
  watched path is touched; the section is absent when no lane watches anything.
- Design note: one consolidated tool by design — four specialized getguards/getgates/
  getnotes/gettombstones lookups would multiply the discovery surface for a single
  moment-in-time need; `list_entries` stays the exploration tool, this is the start ritual.
- `notesLimit` defaults to 10; `includeTombstones=false` trims the graves section; categories
  without entries come back as empty arrays, never omitted keys; entries arrive
  most-recently-updated first.
- The payload also carries a memory-layer teaching block: entries and artifacts point at
  grimoire facts with `pointer: grimoire://memory/<id>` lines; grimoire records carry an
  `overmind://entry/<uuid>` tag back.

## planning_feature_code

- Discovery runs adapter-side in the working directory (read-only `git worktree list
  --porcelain` plus a branch listing). Branches and worktrees whose names carry the feature's
  slug or its NNN prefix (the `feat/NNN-slug` convention) are reported with `kind`
  (worktree/branch), `name`, `path`, and last commit short hash + date. A worktree whose path
  no longer exists is reported `stale`, not dropped. Multiple matches are all listed; no
  match returns an honest empty result plus the naming-convention hint.
- An explicit code pin (`planning_pin_feature_code`) overrides the convention: pinned
  locations are matched against the local listing first and flagged `pinned`, convention hits
  follow deduplicated against the pin, and a pinned location missing locally is reported
  stale — never silently dropped, never auto-cleared. With no pin set the output is unchanged.
- The host's feature read (title, status, priority, updatedAt + `workspaceMatchesProject`)
  rides along in one payload; the host half may fail independently and then the discovery
  half is still answered with `featureError` set. A non-git cwd answers discovery as skipped
  and still returns the feature context.

## ack_entry

- Appends one additive `readBy` row (alias, vendor, at) attributed to the agent session.
- The entry body, its version and UpdatedAt are never touched; reads stay pure
  (`list_entries`/`read_entry` record nothing) and owner-only body writes are unchanged —
  consumption becomes visible without editing the owner's document.
- Acking again with the same identity is an idempotent no-op that keeps the first `at` stamp;
  the echo says "already acknowledged — no change".

## session_identity_set

- Self-serve like `wave_role_set`: session-scoped, calling-session-only. Orchestrator-only
  convention: sub-agents of a live session never call it — one shared session row,
  last-write-wins. Set identity BEFORE claiming a feature.
- The alias is invented client-side per the project's naming rules — see the `agentIdentity`
  block of `overmind_onboard`. Both values are trimmed, max 32 chars, blank refused.
- A 409 means another alive session holds the alias — the error names the holder; invent
  another name and retry.
- A success echoes the stored pair plus warnings, and pins the pair as the adapter's
  reconnect identity: handshakes re-register under it instead of `OVERMIND_ALIAS`/
  `OVERMIND_VENDOR`. Without a registered session (adapter started with no host) the tool
  tells you to restart the adapter with the host running.
- A rename takes the held claims with it. Every feature whose claim is bound to this session id
  re-attributes to the new pair — `ClaimedByAlias`/`ClaimedByVendor` only: `ClaimedAt` and
  `ClaimSessionId` stay put, so the hold does not restart because the alias did — and each feature
  logs one line, `claim re-attributed: <old alias> by <old vendor> → <new alias> by <new vendor>`.
  The response echoes the slugs that moved; an identical pair moves nothing and logs nothing.
- The span ledger is append-only, so the rename closes the window that carried the old pair and
  opens the current one under the new pair with the *original* hold start: the rescue digest reads
  the open span's claim time as "held since", and a rename is not a new hold. No open span for a
  feature (claim written outside the claim door) leaves the ledger untouched.
- A foreign holder's claims are never moved by your rename: they keep their claim-time attribution
  until that holder ages out — see the `claimChurn` block of `overmind_onboard` to see them first.

## wave_role_set

No additional prose — the tool description lost nothing.

<!-- region:others:end -->
