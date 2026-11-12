---
name: overmind-ceremony
description: |
  Owns all planning ceremonies on the Overmind board — via the MCP server or the `overmind planning`
  CLI: feature board, spec/plan/task/result artifacts, status lifecycle, validation gates,
  agent<->human notifications, and the sub-agent briefing contract. Replaces the superpowers plugin entirely — its techniques (Socratic questioning,
  plan decomposition, root-cause debugging, TDD scope, verification) live here as inline rules.
type: prompt
whenToUse: |
  Whenever any planning ceremony runs: creating features, writing spec/plan/task artifacts,
  moving statuses, validating gates, briefing sub-agents on board artifacts, or asking the human
  via overmind notifications. feature-orchestrator and every other skill defer to this file for
  anything that touches the Overmind board.
---

# Overmind Ceremony Skill

Single source of truth for HOW this crew runs planning ceremonies on the Overmind board — over
MCP or the `overmind planning` CLI. Other skills do NOT call `planning_*` / `overmind_*` tools
(or CLI verbs) on their own initiative — they route through the rules in this file.

## Hard Requirement (DO NOT SKIP)

An overmind transport is a **hard prerequisite**: MCP **or** CLI — two transports, one ceremony.
The board stays the single source of truth either way, and both doors pass the same server-side
rules. First action of any ceremony:

1. Call `overmind_onboard` — it resolves the project from cwd, returns `projectId`, board summary,
   materialization block (strategy/targetDir/lastRun) and validation block (`humanGate`).
2. If the `mcp__overmind__*` tools are not available in this session, probe the CLI door
   (`overmind planning ...` — see the CLI Transport section below). CLI present → run the
   ceremony through it. That is not a degraded fallback — it is the same doors over HTTP.
3. Both transports absent → **STOP**. Tell the user:
   "Ни MCP, ни CLI overmind в этом рантайме нет. Церемонии планирования без них не работают —
   настрой MCP-сервер overmind (или CLI `overmind planning`) и вернись."
   Do NOT fall back to hand-written BACKLOG files.

### Worktree pinning (explicit act)

cwd-based resolution breaks inside git worktrees: the registered path points at the main
checkout, so a fresh adapter session fails to resolve — or worse, `overmind_onboard`
auto-creates a phantom project from the worktree cwd. If you work in a worktree, pin it:
write `<worktree-root>/.overmind/project` containing the project id — a raw GUID line or
`{ "projectId": "..." }`. Pinning is an explicit act: an agent that resolved via
`OVERMIND_PROJECT_ID` (or an explicit projectId) while sitting in an unregistered worktree
cwd may write the file; the host never writes repo files on its own. Resolution order is
env > pin file > registry path match; pin discovery stops at the worktree's own `.git`, so a
nested worktree never inherits the outer checkout's pin. Onboard over a worktree of a
registered project refuses to auto-create, names the matched project and instructs pinning.

## CLI Transport — `overmind planning`

The CLI is the second door to the same ceremony seam. It talks HTTP to the running Overmind host
(`OVERMIND_API_URL` → `~/.overmind/host.port` → `127.0.0.1:4761`), so every mutation passes the
same server-side filters as MCP: ClaimLock, log markers, human-gate logic. It exists for MCP-less
runtimes, scripts and CI — not as a way around the rules.

```
overmind planning <verb> [...args] [--json]
```

- Output: compact text on stdout; `--json` for machine parsing — the two agree.
- Exit codes: `0` success · `1` runtime failure (host not running — output carries a
  "start the host" hint — HTTP error, contract refusal like a 409) · `2` usage error.
- A host that is not running is `exit 1` with a hint, never a STOP: the CLI door is itself
  the answer to "no MCP".

### Command map (ceremony loop → verbs)

| Ceremony step | MCP tool | CLI verb |
|---|---|---|
| Projects | `list_projects` | `projects list` |
| Board queue / features | `planning_task_queue`, `planning_list_features` | `queue`, `features list` |
| Feature read (full) | `planning_read_feature` | `feature read {slug}` |
| Current task | `planning_current_task` | `current {slug}` |
| Lanes | `planning_list_lanes` | `lanes` |
| Artifact read / diff | `planning_read_artifact`, `planning_read_artifact_diff` | `artifact read {alias} [--sections ...]`, `artifact diff` |
| Materialization state | `planning_materialize_status` | `materialization-status` |
| Claim / unclaim | `planning_claim_feature`, `planning_unclaim_feature` | `claim {slug}`, `unclaim {slug}` |
| Artifact upsert / patch | `planning_upsert_artifact`, `planning_patch_artifact` | `artifact upsert`, `artifact patch` |
| Task batch (atomic) | `planning_upsert_tasks` | `tasks batch` |
| Ceremony batch (atomic, one feature) | `planning_batch` | — (no CLI in v1) |
| Log append / edit | `planning_append_log`, `planning_edit_log` | `log append`, `log edit` |
| Task status move | `planning_set_status` (task) | `task status` |
| Feature update | `planning_update_feature` | `feature update` |
| Directives append / ack | `planning_append_directive`, `planning_ack_directive` | `directive append`, `directive ack` |

The rows down to `materialization-status` are reads — they run without a session. The rows from
`claim`/`unclaim` down are draft mutations — they require a session (next subsection).

**Not in CLI v1** — human-gate verbs stay on MCP / the human UI door: `validate`
(approval-proof flow), feature `status done` (human tap), `set_feature_priority`
(user-approval convention).

### Session (mutations only)

- `OVERMIND_SESSION_ID` set → the CLI reuses that session.
- Not set → the CLI auto-registers one from `OVERMIND_ALIAS` / `OVERMIND_VENDOR` — same
  defaults as the adapter (`agent` / `unknown`); set both to real values before claiming
  (the CLI door is env-based by design — MCP agents use `session_identity_set` instead),
  per the identity rules in The Ceremony Loop.
- Claim staleness is unchanged: run-and-exit CLI sessions honestly go stale. A long CLI-only
  workflow keeps `OVERMIND_SESSION_ID` alive or re-claims — same 409 recipe as MCP.

### STOP authority

STOP / HELD lines in CLI text output are authoritative — exactly as in MCP renders. A STOP from
`current {slug}` or a refused mutation naming its holder is surfaced to the user verbatim and
never worked around. The transport changes; the authority does not.

## Source of Truth Model

- **Board (Overmind DB) = the only source of truth** for features, artifacts, statuses, entries.
- **`BACKLOG/` on disk = read-only materialized mirror**, rewritten by Overmind after every
  planning mutation (when materialization strategy is `on` — check the onboard block).
- **Never hand-edit materialized files.** All writes go through ceremony doors — MCP tools or
  CLI verbs (same server-side rules). A clobber guard protects local edits, but managed files
  belong to the materializer.
- No `BACKLOG.md` index exists (retired, 049) — the board (web UI / MCP) is the only live state
  copy; the `BACKLOG/` tree holds the materialized contracts, and statuses read from a file are
  stale by definition — check the board.

## Capability Matrix (verified against the Overmind project's own board, 2026-10)

**Works — safe to rely on:**
| Capability | Tools | Board proof |
|---|---|---|
| Feature lifecycle (draft→ready→inProgress→review→done) + auto-transitions; agent done = result + fresh human tap | `planning_create_feature`, `planning_set_feature_status` (agent door: `done`/`inProgress` only) | 006, 019, 023, 043, 109 |
| Claims + session lock (claim/unclaim, `X-Overmind-Session` on 13 feature mutations, EOF auto-release, stale take-over) | `planning_claim_feature`, `planning_unclaim_feature` | 099; live dogfood 6/6 |
| Artifact deletion (agent: draft-only; human: unrestricted; blockedBy edges refuse first) | `planning_delete_artifact` | 094 |
| Spec/plan/task/result artifacts with XML sections | `planning_upsert_artifact` | 006 |
| Validation gate (humanGate off by default; chat approval → validate) | `planning_validate` | 055 |
| Status moves with mandatory log note | `planning_set_status` | 043 |
| Partial reads (sections) + patch writes (section replace, AC tick) + log append + newest-own-line log correction | `planning_read_artifact`, `planning_patch_artifact`, `planning_append_log`, `planning_edit_log` | 039, 040, 106 |
| Current-task resolver (STOP lines are authoritative) | `planning_current_task` | 013 |
| Agent→human notifications with deep links + reply path | `overmind_notify`, `overmind_replies` | 020 |
| Entries: note/decision/gate/guard/featureRequest/tombstone (skills via UI only) | `write_entry`, `read_entry`, `update_entry`, `list_entries`, `search` | 002, 079 |
| Start-of-work briefing — guards/gates with full bodies, decisions/notes as snippets, tombstone list in one call | `overmind_briefing` | 052 |
| Materialization to `BACKLOG/` with clobber guard | automatic after mutations; status in `overmind_onboard` | 033, 054 |
| Batch planning ops (`planning_upsert_tasks`, atomic all-or-nothing) | `planning_upsert_tasks` | 044; live on 007 (7 task artifacts, one call) |
| Batch ceremony ops — patch + append_log + set_status + upsert_tasks + notify on ONE feature in one call; transaction all-or-nothing (a failing op rolls back everything, error names `ops[i]`); per-op guards, claim lock and human proof preserved; `validate` deliberately not an op (proof is request-scoped); batch-level `operationId` replay; post-commit hook failures surface as `warnings[]` on the committed response; ops cannot reference artifacts CREATED by earlier ops (server-side Guids are unnameable pre-call) | `planning_batch` | 225 |
| Structured feature dependencies (blockedBy/blocks) | `planning_add_feature_dependency`, `planning_remove_feature_dependency`, `planning_list_feature_dependencies` | 030; wired on 007 |
| Agent-side import of materialized feature folders | `planning_import_feature` | 058 (OM-7) |
| Mirror export into an explicit empty target (commit-alongside snapshot; zero tracking rows) | `planning_export_backlog` | 075 |

| Artifact version diff as a unified diff (read-only; omitted pair = the two most recent versions; a trailing newline is ignored, so "x" vs "x\n" diffs as empty) | `planning_read_artifact_diff` | 045 |
| Pointer metadata projection (artifact responses and roster rows carry additive `pointers: [{path, anchor}]` — the server never reads the targets) | `planning_read_artifact`, `planning_list_artifacts` | 046 |
| Feature code discovery + explicit pin (read-only scan for worktrees and branches carrying the slug or its NNN prefix; a pin `planning_pin_feature_code` overrides the convention — pinned locations list first flagged `pinned`, convention hits follow deduplicated, a pin missing locally is reported stale, never auto-cleared) | `planning_feature_code`, `planning_pin_feature_code` | 126, 134 |
| Wave read projection + collision inputs — one-call snapshot (`overmind_wave_snapshot` №50: `planning_task_queue` rows verbatim, per-claim identity — alias, vendor, claimState, claimedAt, claimedByHuman —, code locations enriched adapter-side with the feature-code discovery, open blockers rolled up per feature, coordination entries by tag, default `wave-sync,lane-marshalling`, `entryLimit` 1–100 default 20); lane rosters with live holder projection (`planning_set_lanes` full-replace / `planning_list_lanes` №51/52); `lane`, `laneHolder`, `potentialOverlaps`, `openDirectiveCount` projected on queue and board rows; `lane` + `codeSurfaces` globs settable via `planning_update_feature`; claim-time lane check (409 on held lane, `overrideLaneCheck=true` escape hatch echoed back) | `overmind_wave_snapshot`, `planning_set_lanes`, `planning_list_lanes`, `planning_update_feature`, `planning_claim_feature` | 137 |
| Directive channel — the one write that reaches a live claim holder: any session appends to ANY feature regardless of claim or status (append-only, ≤500 chars, session-attributed, HOLD/SYNC/YIELD-style), holder or any reader acks (idempotent), acked leave the `openDirectives`/`openDirectiveCount` projections; coordination entries confirm back the same way — `ack_entry` (№55) appends one additive `readBy` row [alias, vendor, at] per ack, explicit, idempotent, reads stay pure; mutates no artifacts, no statuses, no versions — the 409 claim lock stays intact | `planning_append_directive`, `planning_ack_directive` (№53/54), `ack_entry` (№55) | 137 |
| Wake channel — outbound visibility for idle sessions: `overmind_wake_peek` (side-effect-free peek of pending signals — human replies on this session's notifications + wave messages mentioning its alias, cursor-paged, watermarks untouched) over `GET /api/agent/wake`; the adapter's wake watcher escalates 60s idle → 5s in-gate and surfaces signals as an MCP stdio logging notification plus a pending-signal count on the next tool response — visibility, never turn injection; the wake ritual (self-scheduled tick) and the reply auto-act flag `AutoActOnReplies` (per project, default off) are documented in **Wake ritual (190)** below | `overmind_wake_peek` | 190 |

**Does NOT exist yet — never promise, never emulate silently:**
| Missing | Board ref | Interim rule |
|---|---|---|
| Direct CLI door to planning ops (agent access without the MCP server) | 133 (planned) | Every planning interaction runs through the overmind MCP server — no shell fallback |

(Former rows shipped and moved to the matrix above: entry tombstones, entries materialization
to `ENTRIES/`, worktree/branch ↔ feature binding — feature 134. Do not resurrect them here.)

If a needed capability is in the "missing" list — check the Overmind board first
(`planning_list_features` on the Overmind project); if it shipped, update this matrix.

## The Ceremony Loop

```
overmind_onboard → overmind_briefing → planning_list_features → planning_current_task
      → work (per artifact kind) → planning_set_status(+logNote) / planning_append_log
      → repeat → result artifact → feature to review/done
```

Rules:
- **Claim the feature you are working on** (`planning_claim_feature`) the moment it enters active
  work — before the first feature mutation, not after. The claim is how every runtime and every
  session knows what is in flight; full practice in **Claims workflow** below.
- Identity first, claim second. `session_identity_set(alias, vendor)` sets the calling
  session's identity at runtime — the adopt/bind door for lane aliases and the restore door
  for your stable name. The alias is your own STABLE personal name — pick it per the project's
  naming rules from the agentIdentity block of the overmind_onboard payload, and reuse the SAME
  alias in every session so board history stays attributable. Vendor is your runtime (claude,
  qwen, …); two agents on the same runtime must not look identical — differentiate by alias.
  The defaults (agent / unknown) are placeholders: set a real identity BEFORE claiming — a
  mid-hold change is allowed but warns, and existing claims keep their claim-time alias.
- Identity is set by the ORCHESTRATOR only. Sub-agents share one adapter session row — a
  sub-agent calling the tool would hijack the orchestrator's identity (last write wins); they
  never call it. A live foreign session holding the alias → 409; a stale holder frees it.
  env `OVERMIND_ALIAS` / `OVERMIND_VENDOR` are back-compat bootstrap only (env > placeholder);
  there is no identity file — the board's lane `boundAlias` is the durable home.
- Name your surfaces the moment you set your identity: put the alias into your terminal tab
  title (`printf '\033]2;<alias>\007'` — tmux and terminal tabs), and lead your first messages
  with the alias so background-job lists attribute you (their titles are derived from message
  text; there is no agent-facing session-title API).
- A **STOP line** from `planning_current_task` is authoritative — surface it to the user verbatim,
  never work around it.
- Every status move carries a `logNote` explaining WHY. Unexplained moves are rejected by design.
- Artifact statuses — spec/plan/result: `draft` → `validated` (terminal). Task: `todo` →
  `inProgress` → `review` → `done` (or `blocked`). Closing a task `done` with unticked AC items
  passes with a warning — tick ACs via `planning_patch_artifact` first.
- `validated` artifacts are FROZEN. Changes only via `planning_patch_artifact` (section replace /
  AC tick) with a mandatory `logNote`, or a full re-upsert with user re-validation.

### Claims workflow (099)

- **When** — claim (`planning_claim_feature`) the moment a feature enters active work, before the
  first feature mutation (artifact upsert, status move, priority move). Unclaim
  (`planning_unclaim_feature`) when you hand the feature off or close the wave.
- **Who** — the orchestrator session claims. Sub-agents inherit the lock through the shared
  adapter session; they never claim or unclaim themselves.
- **Identity** — alias+vendor come from the `agentIdentity` block of the onboard response;
  follow the project's naming rules stated there.
- **409 recipe** — a refused mutation names the holder. Holder session live → ask the user before
  taking over. Session stale/ended → take-over is legitimate (re-claim with the same call).
- **EOF auto-release** — a dead adapter process releases its claim on its own; there is nothing
  to clean up afterwards.
- **Coordinator channel (137)** — a wave marshal or lane neighbor reaches a live holder WITHOUT
  the claim: `planning_append_directive` lands a HOLD/SYNC/YIELD line the holder sees as
  `openDirectives` on `planning_current_task` / `planning_read_feature` and acknowledges with
  `planning_ack_directive`. Append-only, nothing else touched — the channel adds
  coordination, it never bypasses the 409 lock. Acks are a cooperative channel, not
  tamper-proof: any session may ack, attribution is honest but not enforced. The `readBy`
  response shape mirrors the directive `AckProjection` by design — a contract-boundary copy,
  change both together — and identity comparison is case-sensitive (`Billy` and `billy` are
  different readers).

### Coordination transport map (162)

Four channels with different jobs — pick by intent, do not mix them up:

- `wave_chat_post` / `wave_chat_unread` — **flow**: the per-lane append-only room (plus the
  project `all` room) for signal-only coordination; etiquette lives in `wave-marshal`.
- `planning_append_directive` — **addressed**: one feature, one claim holder, a command the
  holder acks.
- `overmind_notify` — **personal**: agent↔human gates only (questions, approvals), never
  agent↔agent.
- `overmind_wave_snapshot` — **state**: the one-call read of who holds what and where code
  lives — a picture, not a message stream.

Identity on every channel: posts and log lines carry the calling session's registered
identity — `session_identity_set` sets alias+vendor at runtime (the `agent`/`unknown`
placeholder is noise), and `wave_role_set` sets your role at runtime (worker default,
marshal for the wave lead). The owner sees real aliases in role colors in the wave chat.

## Gates (human approval points)

1. **Spec + plan gate (one tap)** — write the spec, then the plan from it, and present
   BOTH in one approval notification (artifact ids + options + recommendation). One fresh
   human reply validates the spec AND the plan of the same feature in one pass
   (`planning_validate` twice against the same reply; any body edit to either voids it for
   both). No task breakdown before both artifacts are validated. The two-gate sequence
   (spec → tap → validate → plan → tap → validate) is the fallback when the user asks for
   per-artifact control or the plan depends on spec-gate feedback.
2. **Feature done** — two prerequisites, both enforced server-side (409 teaches the fix):
   (a) a `result` artifact must exist on the feature; (b) a FRESH human tap — `overmind_notify`
   kind=`approval` → the user's reply must be newer than the result's last update (same proof
   mechanics as validation gates; editing the result voids the tap). The done log line carries
   reviewed-by provenance automatically. The human UI door needs the result but no tap.
   The code lands through a PR: result artifact → branch pushed and `gh pr create` into the
   main GitHub repo → the human approves the PR (GitHub UI or explicit approval in chat) → the
   agent merges (`gh pr merge --merge`, branch deleted) → the done tap closes the feature.
3. When `humanGate=on` (validation block in onboard), `planning_validate` returns STOP —
   the human approves in the Overmind UI; do not retry in a loop, notify and wait.
4. **Gate presentations are Socratic too** — an approval request is never a bare «ok?»: artifact
   summary + explicit options (approve / change X / rework Y) + your recommendation.
5. **Gate reply polling is mandatory.** After sending `overmind_notify` with kind=`approval` or
   kind=`question`, the session MUST schedule a recurring reply check every 5-10 minutes and keep
   it until the human's reply arrives - then act on it (validate / rework / flip) and cancel the
   tick. Firing a gate and silently idling is a violation. Claude Code: session cron (`CronCreate`)
   prompting an `overmind_wake_peek` / `overmind_replies` check; other runtimes: their scheduler
   equivalent (the Wake ritual below is the mechanics).

## Wake ritual (190)

Overmind has no host→agent push: MCP adapters are stdio clients that open every connection
themselves, and a dead process has no channel — the Host cannot wake a sleeping model. What
the platform ships is a cheap side-effect-free peek plus a visible signal; the tick is the
session's own job:

- **The pattern — live-but-idle sessions keep a self-scheduled check.** While a session idles
  with work pending (an approval it awaits, a wave it marshals), it schedules a recurring tick
  that calls `overmind_wake_peek` and acts on what comes back. Claude Code: a session cron via
  `CronCreate` with the prompt "check overmind_wake_peek, act on signals". Other runtimes:
  their scheduler equivalent — the MCP tool name is identical everywhere. The adapter helps
  within its means: its wake watcher polls every 60s idle and escalates to 5s while in the
  gate, surfacing signals as an MCP logging notification into the stdio stream plus a
  pending-signal count on the next tool response.
- **Honest limits.** No process, no channel — a dead or exited session is unreachable; after
  a restart, re-peek. The stdio log line is visibility, not injection: nothing turns the model
  on. A sleeping session wakes only from its own tick.
- **`overmind_wake_peek`** — session-scoped, read-only: human replies on this session's
  notifications (kind `notification-reply`) and wave messages mentioning its alias (kind
  `wave-mention`), ordered, cursor-paged, caps at 100 per page. Advances no watermarks and
  marks nothing read — consume mention traffic with `wave_chat_unread` as usual.

### Auto-act on replies (`AutoActOnReplies`)

Per-project flag, **default OFF** — off means today's behavior: a reply is just a DB row the
agent picks up on its next poll. On, a human reply on an approval notification performs the
gated action itself, the reply being the human proof under the same 178 freshness rule (it
must be newer than the artifact's last content update):

- the notification targets a tab (`spec`/`plan`/`result`) → the draft artifact of that kind
  validates itself;
- the notification carries no tab → the feature flips done — only when a validated result
  exists, no blocker is unclosed and the feature is not done already; every other case is
  skipped and logged, never guessed.

Every performed action leaves an audit line (`[auto-act] by human reply <id>`) and sends the
authoring agent an info notification. Recovery is the human's repeat tap: the queue is
in-memory and dies with the process, so a lost item is recovered by replying to the same
notification again — a fresh reply re-stamps `RepliedAt` and re-queues it.

## Working the Artifacts

- **Feature intake**: `planning_create_feature(slug=NNN-slug, title, priority, body)` —
  body = durable what/why (goal, requirements, non-goals). Slug numbering follows the board's
  existing sequence; check `planning_list_features` first.
- **Spec**: `planning_upsert_artifact(kind=spec)` — built through Socratic questioning
  (see Techniques). Frozen after validation.
- **Plan**: `planning_upsert_artifact(kind=plan)` — decomposition per Techniques. Frozen after
  validation.
- **Tasks**: `planning_upsert_artifact(kind=task, order=N, brief=..., review=light|hard)` —
  body with XML sections `<goal>`, `<scope>`, `<anti-scope>`, `<constraints>`,
  `<acceptance-criteria>` (checkbox items), `<refs>`, `<obstacles>`, `<log>`.
- **Tasks (batch)**: `planning_upsert_tasks` creates/updates N task artifacts atomically.
  Known quirk (Overmind bug note `6ee3be0f`, 2026-09): batch-created task bodies register
  only some XML sections — later `planning_patch_artifact(section=obstacles|refs|...)` can
  400 with "Section not found". Workaround: stash findings via `planning_append_log`.
- **Result**: when all tasks are done — `planning_upsert_artifact(kind=result)` summarizing
  outcome, deviations, follow-ups.
- **Task close — blast-radius contract (R1):** a task whose `<scope>` changes a symbol must close with one of: (1) the `find_references` caller list for every changed symbol, recorded in the task `<log>`/`<obstacles>` (a `file:line` row list — the tool's native output shape); or (2) the reason no list exists, chosen from a checkable set: `symbol not indexed` · `zero callers reported` · `non-code change (config/docs/migration wiring)` · `worktree/fixture code only`. The review gate checks presence-of-artifact, not quality of judgment — a wrong caller list is a review finding; a missing one is a gate failure.

**Batch recipes (225)** — one turn per ceremony step via `planning_batch(featureSlug, ops[])`; every op all-or-nothing, one result row per op:
- **Task close**: one batch = patch (tick the AC) + append_log + set_status(done), addressed by `artifactId` or `alias` (`T<order>`, `S`/`P`/`R`) — was three sequential calls. AC-tick parity with the standalone calls is test-pinned (Infrastructure 225 suite).
- **Gate present**: one batch = append_log + notify(kind=approval, text=…) — the log line and the approval ask land together, the human gets the deep link (featureSlug defaults to the batch feature). `validate` stays its own call: proof is request-scoped and deliberately not an op.
- Rules: ops may only reference artifacts that exist BEFORE the batch (new artifacts' Guids are unnameable pre-call); a failing op rolls back the whole batch and names `ops[i]`; pass a fresh `operationId` per logical batch for replay.

## Sub-Agent Briefing Contract (MANDATORY)

Sub-agents in this crew's runtimes inherit the host's overmind transport — MCP tools, or the
`overmind planning` CLI where MCP is absent. They CAN and MUST work artifacts through Overmind,
not through disk files.

- Orchestrator hands the sub-agent: `projectId` (or "resolve via `overmind_onboard`"),
  `featureSlug`, and the task `artifactId` (or order number).
- Sub-agent reads ONLY its task artifact: `planning_read_artifact(artifactId)` — full body,
  or `sections=` for cheap partial reads. spec/plan stay with orchestrator + reviewer;
  the feature body is readable by all.
- Sub-agent writes back: `<log>` lines via `planning_append_log`; discovered workarounds/quirks
  via `planning_patch_artifact(section=obstacles)`; AC ticks via
  `planning_patch_artifact(acIndex|acText)`; status moves via `planning_set_status` with logNote.
- Disk copies under `BACKLOG/` are for humans and for tools without a transport. A sub-agent
  with overmind MCP or CLI never reads its task from disk.
- If the sub-agent runtime has neither overmind MCP nor the CLI → STOP and report (hard
  requirement above). With CLI only, the same contract runs over the verbs in the command map —
  the orchestrator states in the briefing which transport to use; read/patch/log/status map 1:1.
- **Indexed-symbol gate:** before the first edit of an indexed symbol, run `find_references` on it. The caller list lands in the task artifact's log (or `<obstacles>`); a review that finds an edited symbol without a recorded caller list — or without a recorded reason why the symbol has no indexed callers (not indexed, zero callers, config-only change) — returns the task. The exception list is checkable by field, not by trust (ADR-003 §4).

## Techniques (replacing the superpowers plugin — inline, no external dependency)

**Socratic spec questioning** (replaces superpowers:brainstorming). Ask ONE question at a time —
and every question is SOCRATIC by the iron rule: it ships with 2–3 concrete proposed answers plus
your recommendation; the user picks, edits or overrides. A bare question without proposed answers
is a violation — social talk is the only exception.
Probe: real user pain, edge cases, data ownership, failure modes, what is explicitly OUT of scope.
Stop when the spec artifact holds no ambiguity a coder could resolve two different ways.
Output lands ONLY in the spec artifact — never in side files.

**Plan decomposition** (replaces superpowers:writing-plans). Tasks are small (one concern each),
name exact file paths, and every task carries a verification step in `<acceptance-criteria>`
(a command, a test, an observable behavior). If a task has no verifiable AC — split it further.

**Root-cause-first debugging** (replaces superpowers:systematic-debugging). No fix without a
reproduced root cause written into the task's `<obstacles>` or the bug intake artifact.
Guessing fixes is forbidden.

**Verification before completion** (replaces superpowers:verification-before-completion). Never
report "done" on "should work". Prove: build green, tests green, the user's original scenario
exercised end-to-end. State plainly what could not be verified.

**TDD scope** (was AGENTS.md superpowers rule 4). Strict red-green-refactor applies to
domain/business logic and non-trivial algorithms only — NOT to EF migrations, DI wiring, config,
DTOs, glue code. When in doubt, ask the user before choosing TDD on/off for a task.

**Parallel dispatch** (replaces superpowers:dispatching-parallel-agents). Independent task
artifacts may run as concurrent sub-agents: batch every spawn in ONE message, each briefed
per the Sub-Agent Briefing Contract (projectId, featureSlug, artifactId). Two tasks go
parallel ONLY when their file sets are disjoint — a shared file or a read-after-write
dependency means sequential. Collect every result before the next ceremony move; a failed
sub-agent does not cancel its siblings — the failure lands in that task's `<obstacles>`
and the task is re-dispatched alone.

**Workspace isolation** (replaces superpowers:using-git-worktrees). One feature = one branch
`feat/NNN-slug`. Two concurrent writers never share a checkout — the second writer gets its
own `git worktree` (plain shell where the runtime has no native tool). Never isolate on top
of uncommitted work you do not own: dirty state is committed or handed back to the user first.
Worktree doctrine: a LANE owns one worktree — the resident body (`.claude/worktrees/<lane>`).
Features inside a lane work in the lane worktree BY DEFAULT; a feature's own worktree is an
explicit decision (risky surface, parallel sub-waves). Standalone lane-less features get
their own worktree. On feature close the feature's own worktree is removed — nothing stale
kept; the lane worktree persists for the next feature in the lane.

## Relationship to Other Skills

- `feature-orchestrator` owns the pipeline (phases, review gates); this skill owns the ceremony
  mechanics (which MCP call, which status, which gate). Orchestrator defers to this file for
  every board interaction.
- Bug reports arrive AS task artifacts (goal = expected behavior, scope = what's broken,
  AC = fix verification) — quick-fix route, no spec/plan gates.
- Simple tasks = a board feature with a single task artifact, no spec/plan (same shape the
  Overmind project itself dogfoods).
- `audit-orchestrator` reads project state from the board, not from disk files.

## Communication

- **Speak Russian.** Report ceremony moves compactly: "Фича `004-foo` → inProgress, таска
  `01-bar` взята в работу", "Spec validated, гейт пройден".
- STOP lines and blocked statuses are surfaced verbatim, never softened.
- **Output contract (MANDATORY):** end with the fixed block, then STOP:
  1. **Done** — summary + artifact identifiers (feature slug, artifact ids).
  2. **Next step** — recommended follow-up per `SKILLFLOW.md`.
  3. **Alternatives** — 2–3 other valid follow-ups.
  Suggest, never auto-proceed. The user picks the next step.
