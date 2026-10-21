# SKILLFLOW.md — Workflow Graph

Every skill reads this file before suggesting its «next step» (see Skill output contract in AGENTS.md).
The graph suggests — the user decides. Skills NEVER auto-proceed to the next step.

**All planning state lives on the Overmind board (MCP or the `overmind planning` CLI).** Ceremony mechanics (probe, lifecycle,
gates, sub-agent briefing, capability matrix) are owned by the `overmind-ceremony` skill.
`BACKLOG/` on disk is a read-only materialized mirror — never hand-edit it.

## Main Route (Complex feature)

```
0.0 requirements-intake (OPTIONAL) ──► 0.1 spec gate ──► 0.2 plan gate ──► tasks ──► 2 implement ──► 3 review ──► 4 fix? ──► 5 arch gate
     (messy raw inputs            (USER              (USER            per       (coder per       (2-stage:        (loop back      (end of
      → intake notes)              APPROVES →         APPROVES →       task       task artifact)   spec + code)     to 2)          phase only)
                                   planning_validate) planning_validate)
```

| Step | Skill / Owner | Artifacts (Overmind board) | Gate |
|------|---------------|-----------|------|
| 0.0 Intake (OPTIONAL, trigger-based — only for messy raw inputs) | feature-orchestrator (intake mode) | notes in feature body / entry | User confirms requirements are complete |
| 0.1 Spec | feature-orchestrator + Socratic questioning (`overmind-ceremony` techniques) | spec artifact (`planning_upsert_artifact`) | **USER APPROVAL → `planning_validate` — frozen after** |
| 0.2 Plan | feature-orchestrator + plan decomposition (`overmind-ceremony` techniques) | plan artifact | **USER APPROVAL → `planning_validate` — frozen after** |
| Tasks | feature-orchestrator | task artifacts with `<goal>/<scope>/<anti-scope>/<constraints>/<acceptance-criteria>/<refs>/<obstacles>/<log>` | — |

The feature is created on the board (`planning_create_feature`) when it enters the backlog, **before Phase 0**.
| 2 Implement | `dotnet-coder` / `web-coder` / `node-coder` / `js-platform-coder` / `maui-coder` (strong execution model) | code + `<obstacles>`/`<log>` patched into the task artifact | `nuget-licensing-guard` before any NuGet change |
| 3 Review | `dotnet-code-reviewer` / `web-code-reviewer` / `node-code-reviewer` / `js-platform-code-reviewer` (strong execution model); depth from the task's `review:` flag — `light` (default) = code reviewer, `hard` = + `architect-review` (Light checklist) | `audits/review-NNN-NN.md` + audit-entry on the board (first line = machine-parsable verdict per the GEMINI.md provenance rule, `feature:NNN` tag, pointer to the file) | BLOCKER → Phase 4; Security BLOCKER → STOP, human only |
| 4 Fix loop | coder ← review comments | updated code | back to Phase 3 |
| 5 Arch gate | `architect-review` Hard checklist (end of sprint phase / big refactor only) | `audits/architect-review-YYYY-MM-DD.md` + ADR in `TECHNICAL-SOLUTIONS.md` | — |

## Coder lanes (routing for Implement/Review)

| Lane | Owns | Never touches |
|------|------|---------------|
| `web-coder` | Next.js/React FULL-STACK apps: UI + RSC + Server Actions + BFF + API routes; SPAs; UI component libraries | standalone Node services; Electron/RN |
| `node-coder` | Standalone Node services (Express and derivatives: Fastify/Nest/Hono), workers, scripts/CLI/tooling, non-UI npm libraries | anything inside a Next.js repository; UI |
| `js-platform-coder` | Electron + React Native: main/preload processes, native modules, navigation, multi-platform (2–3 targets) builds | web and pure-backend code |

Monorepos containing a Next app + a standalone Node service: both coders, tasks split by app/repo boundary (never by file inside the Next app).

## Alternative Routes

**Board planning (sprint):**
`sprint-planner` — board-level planning ONLY: triages the queue (`planning_task_queue`), orders priority bands (`planning_move_feature`; band crossings via `planning_set_feature_priority` ONLY on explicit user approval), wires feature dependencies (`planning_add_feature_dependency` / `planning_remove_feature_dependency`), creates features (`planning_create_feature`), slices sprints/phases, grooms the slice into lanes for a parallel wave (`planning_list_lanes` read → `planning_set_lanes` full-replace roster → `planning_update_feature` lane + codeSurfaces binding, one open feature per lane — the board 409s the rest). Never enters a feature's inner pipeline — implementation hands off to `feature-orchestrator`; phase end hands off to `audit-orchestrator`.

**Parallel wave sync (while a wave runs):**
`wave-marshal` — wave-level coordination ONLY: reads the live lane roster (`planning_list_lanes`) and the whole wave in one call (`overmind_wave_snapshot`), reads claims and progress across lanes, detects lane collisions, serial-in-lane violations, cross-lane file-surface overlap, shared-surface churn, stale claims, blocked sequencing and review limbo, then issues decisions — log directives on unclaimed features, `wave-sync` entries/issues for claimed ones (a 409 naming an alive holder means coordinate, never fight), dependency wiring for sequencing, escalations to the user for authority-gated moves (priorities, statuses, lane re-binds, take-overs). Sits BETWEEN `sprint-planner` (orders the queue before the wave) and `audit-orchestrator` (retro after it); never enters a feature's inner pipeline — that stays with `feature-orchestrator`. Never codes, never reviews code.

**Product strategy (product-level planning):**
`product-strategist` — product-level planning ONLY: product state analysis (board + docs + `audits/`), goal-setting dialogue with the user (durable outcomes as overmind entries), gap analysis (missing / stale / rework), roadmap materialized as board features (`planning_create_feature`) and priority proposals (cross-band moves only on explicit user approval). Upstream of `sprint-planner`: strategy decides WHAT and WHY enters the board; logistics (order, dependencies, sprint slicing, pinning) stays with `sprint-planner`. Never queue mechanics, never a feature's inner pipeline — handoffs: `sprint-planner` (queue) / `feature-orchestrator` (execution).

**Solution architecture (greenfield design, tech selection, big rewrites):**
`solution-architect` — senior-most architecture authority: initial solution architecture (bounded contexts, service decomposition), technology selection with the `TECHNICAL-SOLUTIONS.md` ADR ritual, large additions/rewrites/migrations of services. The stack architects (`dotnet-architect` / `web-architect` / `node-architect`) are its per-stack specialists and escalate cross-stack seams to it. Consulted by `feature-orchestrator` (Phase 1/Phase 2). Never codes, never reviews code, never works task artifacts — implementation hands off to `feature-orchestrator`.

**Quick fix (bug):**
QA/bug report arrives AS a task artifact on the board (goal = expected behavior, acceptance = fix verification) → root-cause-first debugging (`overmind-ceremony` techniques — no fix without reproduced root cause) → coder fix → verification-before-completion → `planning_set_status(done)`. No spec/plan gates. Complex bugs (>3 files) still get Phase 3 review.

**Simple task (<3 files, single layer):**
Board feature with a single task artifact, no spec/plan (same shape the Overmind project itself dogfoods) → coder directly → done.

**Fast fix (cosmetic micro-change):**
`fast-fixer-coder` — 1 file, ≤10 changed lines, cosmetic only (icons, labels, styles, wording, typos in UI strings). Zero paperwork: no board feature, no task artifact, no review gate. The scope gate is absolute — anything wider is REFUSED and handed off to the matching coder; forbidden zones (deps, config, secrets, migrations, security) are never touched. Defects are bugs → the quick-fix route above, never fast-fixer.

**Docs-only:**
`documentation-writer` — skips Phase 3 code review, uses its own verification checklist.

**Naming (any artifact — class, section, service, repo, feature):**
`naming-sensei` — the crew's naming consultant: any agent (coder, orchestrator, reviewer) routes a naming ask to it as a `DOJO:` prompt and gets an instant dry strike in dojo mode — main name + two backups + one-line rationale, zero dialogue. The user's «сенсей…» ask gets the same in crew tone. Heavy names (product/repo/brand) go through the full ceremony: tone calibration → AI-cliche veto → spectrum or elimination tournament (3–6 rounds × 5 names, 1–2 winners, semifinals past 8 survivors) → collision check on finalists. Never applies a name itself — application is coder work.

**Coding challenge / тестовое задание:**
`coding-challenge-orchestrator` — replaces this entire flow with its fixed pipeline.

**System-wide audit (sprint retro, phase end):**
`audit-orchestrator` — outside the feature flow; reads state from the board; outputs `audits/audit-YYYY-MM-DD.md`.

<!-- augur-crew:start (source: D:\Projects\AI-Center\augur-crew\SKILLFLOW.augur.md) -->
**Pre-sales research & estimation (augur-crew):**
`augur-orchestrator` — outside the feature flow, Overmind optional, runs INLINE in the main session (gates need the user). Pipeline:
`augur-researcher` (strong execution model, sourced inventory) → **GATE 1** (user closes open questions) →
`augur-architect` (strongest reasoning model, architecture + stack + phases; consults `solution-architect` in consult-only mode) →
**GATE 2** (user approves architecture and phasing) → `augur-estimator` (strongest reasoning model, classic vs
AI-native hours, FTE, three scenarios, BA/QA delta — md + xlsx per `augur-method`) →
`augur-challenger` (strong execution model, hostile review with routed findings; BLOCKER → re-estimate, max 2 re-estimates / 3 challenges) → one-page
`audits/augur-summary-<client>-<date>.md`. Artifacts: `audits/augur-{research,architecture}-<client>-<date>.md`, `audits/augur-{estimate,challenge}-<client>-<date>-i<n>.{md,xlsx}`.
Hours and FTE only, never money. Deal won → `solution-architect` (ADRs, CONTEXT.md) →
`product-strategist` / `sprint-planner` (phases as board features) → `feature-orchestrator`.
`augur-method` holds the estimation rules (AM-IDs) and the calibration log (edit the repo copy, then `install.ps1`).
<!-- augur-crew:end -->

**Self-healing CI/CD (failing pipeline):**
`pipeline-doctor` — log-reader (strong execution model) → fix (strong execution model) → trigger pipeline → watch → repeat, **max 3 iterations, then STOP and report**. Review of the final fix by a separate agent. Transport: `gh` CLI for cloud GitHub (token scope = security perimeter, never widen it for the agent); `glab`/REST for on-prem Git — transport is swappable, the loop is not. Tell the agent explicitly when the codebase is available locally, or it will read files through the API.

<!-- umbraco-exodus:start -->
**Umbraco migration (version upgrade of an Umbraco CMS solution) — Umbraco Exodus crew:**
`umbraco-exodus-orchestrator` (project-level, installed into the client repo via `umbraco-exodus/Skills/umbraco-exodus/install.sh`) — replaces the main route: `umbraco-exodus-auditor` → user validates audit → plan mode (one board feature per phase) → user validates plan → run mode with `umbraco-exodus-backend-coder` / `umbraco-exodus-backoffice-coder` / `umbraco-exodus-content-migrator` and `umbraco-exodus-verifier` verdicts as phase gates. `feature-orchestrator` hands off when the solution references `Umbraco.Cms` and the request is a migration or upgrade. `umbraco-sensei` dojo mode (`DOJO:`) serves all of them.
<!-- umbraco-exodus:end -->

## Hard Rules (always on, any route)

- Commit by feature: commit at every task close and at review handoff; `git push` only on explicit user request; the `done` flip is the human's door.
- Sub-agents: `strong execution model` (default) / `strongest reasoning model` (orchestration, audit, detection, obfuscation) only, selected in Agent Manager. Never inherit the main conversation's model for delegated work.
- Executing sub-agent reads ONLY its task artifact (via `planning_read_artifact` or the matching CLI verb — never from disk when MCP or the CLI is available). spec/plan artifacts belong to orchestrator + reviewer; the feature body is readable by all.
- Frozen artifacts (validated spec/plan) change only via `planning_patch_artifact` with `logNote` or re-validation with the user. Removed content gets a `<removed>` tombstone, never silent deletion (context-clash defense).
- Overmind (MCP or the `overmind planning` CLI) is a hard requirement for planning ceremonies — no MCP → use the CLI, neither → STOP, no file-based fallback.
- Cloud CLI mutations (`gcloud`/`az`/`aws`) are never autonomous — user approval every time; infra goes through IaC.
- Every skill ends with the output contract: Done → Next step → Alternatives → STOP.
- Every work question to the user is SOCRATIC — the question ships with 2–3 proposed answers + a recommendation; the user picks. Bare questions (no options) are social-talk-only, never work (spec/plan/augur gates included).
