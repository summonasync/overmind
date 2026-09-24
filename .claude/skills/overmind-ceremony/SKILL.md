---
name: overmind-ceremony
description: |
  Owns all planning ceremonies via the Overmind MCP server: feature board, spec/plan/task/result
  artifacts, status lifecycle, validation gates, agent<->human notifications, and the sub-agent
  briefing contract. Replaces the superpowers plugin entirely — its techniques (Socratic questioning,
  plan decomposition, root-cause debugging, TDD scope, verification) live here as inline rules.
type: prompt
whenToUse: |
  Whenever any planning ceremony runs: creating features, writing spec/plan/task artifacts,
  moving statuses, validating gates, briefing sub-agents on board artifacts, or asking the human
  via overmind notifications. task-orchestrator and every other skill defer to this file for
  anything that touches the Overmind board.
---

# Overmind Ceremony Skill

Single source of truth for HOW this crew runs planning ceremonies on the Overmind MCP server.
Other skills do NOT call `planning_*` / `overmind_*` tools on their own initiative — they route
through the rules in this file.

## Hard Requirement (DO NOT SKIP)

Overmind MCP is a **hard prerequisite**. First action of any ceremony:

1. Call `overmind_onboard` — it resolves the project from cwd, returns `projectId`, board summary,
   materialization block (strategy/targetDir/lastRun) and validation block (`humanGate`).
2. If the `mcp__overmind__*` tools are not available in this session → **STOP**. Tell the user:
   "Overmind MCP не настроен в этом рантайме. Церемонии планирования без него не работают —
   настрой MCP-сервер overmind и вернись." Do NOT fall back to hand-written BACKLOG files.
3. Antigravity runtime: overmind MCP is expected but not verified — same STOP rule applies.

## Source of Truth Model

- **Board (Overmind DB) = the only source of truth** for features, artifacts, statuses, entries.
- **`BACKLOG/` on disk = read-only materialized mirror**, rewritten by Overmind after every
  planning mutation (when materialization strategy is `on` — check the onboard block).
- **Never hand-edit materialized files.** All writes go through MCP tools. A clobber guard
  protects local edits, but managed files belong to the materializer.
- There is NO `BACKLOG.md` index anymore. State lives on the board (UI/MCP); disk holds only
  the artifact tree. (Overmind board feature 049 confirms this direction.)

## Capability Matrix (verified against the Overmind project's own board, 2026-09)

**Works — safe to rely on:**
| Capability | Tools | Board proof |
|---|---|---|
| Feature lifecycle (draft→ready→inProgress→review→done) + auto-transitions | `planning_create_feature`, `planning_set_feature_status` (agent door: `done`/`inProgress` only), `planning_set_current` | 006, 019, 023, 043 |
| Spec/plan/task/result artifacts with XML sections | `planning_upsert_artifact` | 006 |
| Validation gate (humanGate off by default; chat approval → validate) | `planning_validate` | 055 |
| Status moves with mandatory log note | `planning_set_status` | 043 |
| Partial reads (sections) + patch writes (section replace, AC tick) + log append | `planning_read_artifact`, `planning_patch_artifact`, `planning_append_log` | 039, 040 |
| Current-task resolver (STOP lines are authoritative) | `planning_current_task` | 013 |
| Agent→human notifications with deep links + reply path | `overmind_notify`, `overmind_replies` | 020 |
| Entries: note/decision/gate/guard (skills via UI only) | `write_entry`, `read_entry`, `update_entry`, `list_entries`, `search` | 002 |
| Materialization to `BACKLOG/` with clobber guard | automatic after mutations; status in `overmind_onboard` | 033, 054 |

**Does NOT exist yet — never promise, never emulate silently:**
| Missing | Board ref | Interim rule |
|---|---|---|
| Agent-side import of existing BACKLOG folders | 058 (inProgress) | Human imports via Overmind UI |
| Batch planning ops | 044 (inProgress) | Loop single calls |
| Structured feature dependencies (blockedBy/blocks) | 030 (draft) | Keep dependencies in `<refs>` sections |
| Entry tombstones | 029 (draft) | Mark removals as `<removed YYYY-MM-DD — reason>` in bodies |
| Entries materialization to disk | 051 (draft) | Entries live in DB only — read via MCP |
| Agent briefing tool | 052 (draft) | Briefing = `overmind_onboard` + `planning_list_features` + `planning_current_task` + `list_entries` |
| Worktree/branch ↔ feature binding | 063 (draft) | Naming convention: branch `feature/<slug>` |

If a needed capability is in the "missing" list — check the Overmind board first
(`planning_list_features` on the Overmind project); if it shipped, update this matrix.

## The Ceremony Loop

```
overmind_onboard → planning_list_features → planning_current_task
      → work (per artifact kind) → planning_set_status(+logNote) / planning_append_log
      → repeat → result artifact → feature to review/done
```

Rules:
- Prefer the pinned current feature (`planning_set_current`); ask before switching.
- A **STOP line** from `planning_current_task` is authoritative — surface it to the user verbatim,
  never work around it.
- Every status move carries a `logNote` explaining WHY. Unexplained moves are rejected by design.
- Artifact statuses — spec/plan/result: `draft` → `validated` (terminal). Task: `todo` →
  `inProgress` → `review` → `done` (or `blocked`). Closing a task `done` with unticked AC items
  passes with a warning — tick ACs via `planning_patch_artifact` first.
- `validated` artifacts are FROZEN. Changes only via `planning_patch_artifact` (section replace /
  AC tick) with a mandatory `logNote`, or a full re-upsert with user re-validation.

## Gates (human approval points)

1. **Spec gate** — spec artifact `draft` → user approves (in chat, or `overmind_notify`
   kind=`approval` + `overmind_replies`) → `planning_validate`. No plan work before this.
2. **Plan gate** — same flow for the plan artifact. No task breakdown before this.
3. **Feature done** — `planning_set_feature_status(status=done)` only on the user's explicit
   request in-session (agent door), with `logNote` carrying provenance.
4. When `humanGate=on` (validation block in onboard), `planning_validate` returns STOP —
   the human approves in the Overmind UI; do not retry in a loop, notify and wait.

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
- **Result**: when all tasks are done — `planning_upsert_artifact(kind=result)` summarizing
  outcome, deviations, follow-ups.

## Sub-Agent Briefing Contract (MANDATORY)

Sub-agents in this crew's runtimes inherit the host's MCP tools — they CAN and MUST work
artifacts through Overmind, not through disk files.

- Orchestrator hands the sub-agent: `projectId` (or "resolve via `overmind_onboard`"),
  `featureSlug`, and the task `artifactId` (or order number).
- Sub-agent reads ONLY its task artifact: `planning_read_artifact(artifactId)` — full body,
  or `sections=` for cheap partial reads. spec/plan stay with orchestrator + reviewer;
  the feature body is readable by all.
- Sub-agent writes back: `<log>` lines via `planning_append_log`; discovered workarounds/quirks
  via `planning_patch_artifact(section=obstacles)`; AC ticks via
  `planning_patch_artifact(acIndex|acText)`; status moves via `planning_set_status` with logNote.
- Disk copies under `BACKLOG/` are for humans and for tools without MCP. A sub-agent with
  overmind MCP never reads its task from disk.
- If the sub-agent runtime has no overmind MCP → STOP and report (hard requirement above).

## Techniques (replacing the superpowers plugin — inline, no external dependency)

**Socratic spec questioning** (replaces superpowers:brainstorming). Ask ONE question at a time.
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

## Relationship to Other Skills

- `task-orchestrator` owns the pipeline (phases, review gates); this skill owns the ceremony
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
