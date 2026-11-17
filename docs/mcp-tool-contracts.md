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
  `OVERMIND_WAKE_REPLY_LOOKBACK_DAYS` on the host (values below 1 clamp to 1). The mention arm
  never reads that variable.
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
  - **agentIdentity** — effective alias/vendor (env > placeholder, with its source), the
    project's naming rules for choosing your own stable alias, and the `session_identity_set`
    next step when the alias is still the shared placeholder.
  - **semantic** — search availability, with the verbatim probe reason when down.
  - **adapter** — adapterVersion vs hostVersion, with a restart warning when they diverge or
    the adapter predates version reporting.
- `brief=true` returns only the project id, board counts per status, humanGate and the
  adapter version — the cheap repeat call for agents already onboarded and working a lane.

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

## wave_role_set

No additional prose — the tool description lost nothing.

<!-- region:others:end -->
