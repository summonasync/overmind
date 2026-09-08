---
feature: 006
slug: planning
status: draft
created: 2026-09-08
---

# Spec 006 — Planning (DRAFT, awaiting user validation)

## Goal

Agents run the full development ceremony inside Overmind: features, specs, plans, tasks, and results live in the database as structured artifacts with markdown+XML bodies, creatable and drivable through MCP, editable and approvable in the UI. The database is the source of truth; markdown on disk is an optional, per-project materialized view.

## Decisions

### Data model (approved 2026-09-08)

- `PlanningFeature { Id, ProjectId, Slug, Title, Status, Version, CreatedAt }` — status: `draft / ready / in_progress / done`; `Version` bumps on edits after `ready`.
- `PlanningArtifact { Id, FeatureId, Kind, Status, Body, Order, FrozenAt?, UpdatedAt, Version }` — one table for `Spec / Plan / Task / Result`. `Body` is markdown with XML sections (`<goal>`, `<scope>`, `<anti-scope>`, `<constraints>`, `<acceptance-criteria>`, `<refs>`, `<obstacles>`, `<log>`) — the same grammar our agents already parse.
- Task status enum: `todo / in_progress / review / done / blocked`. Spec/Plan: `draft / validated` (+ `FrozenAt`). `Order` — explicit numeric order of tasks within a feature (replaces the NN filename prefix).
- Body versioning reuses the Entries version-history mechanics.
- Exactly two model-level invariants: status enums and the frozen flag. Everything else is markdown convention.

### Lifecycle & gates

- Flow: feature (draft) → spec (draft) → validate → plan → validate → tasks → statuses → result.
- Editing a validated artifact is allowed but requires a dated change note in `<log>` and resets status to `draft` (re-validation). The model checks the note exists.
- Validation gate is two-level: global `requireHumanValidationDefault: on | off` in Settings (default **off**) + per-project `requireHumanValidation: unset | on | off` (default `unset` = use global; `on`/`off` force and ignore the global). When the effective value is on, `planning_validate` via MCP returns 409 — only the UI Approve button validates. When off, agents self-validate.
- MCP resources tell the agent when to stop: `current` returns an explicit "STOP: spec awaits human validation" state instead of a task when a gate is unmet.

### MCP surface (approved 2026-09-08)

Resources (read):
- `overmind://projects/<id>/planning/features` — compact board: features with task counts/statuses.
- `overmind://projects/<id>/planning/features/<slug>` — full feature: spec, plan, tasks as markdown.
- `overmind://projects/<id>/planning/features/<slug>/current` — first non-done task by `Order`, or a STOP state.

Tools (write): `planning_create_feature`, `planning_upsert_artifact` (create or new version), `planning_set_status` (logNote required), `planning_validate` (409 when human validation is required), `planning_append_log`. The session bootstrap prompt teaches: read `planning/features` → take `current` → work → `set_status` + `append_log`.

### Materialization (approved 2026-09-08, amended: strategy + default off)

- Per-project `planningMaterialization: off | on | archive` — project-level override with `inherit` as the default value.
- Global setting `planningMaterializationDefault: off | on | archive` (default **off**) in Settings; projects on `inherit` follow it.
- `on` → writes `BACKLOG/<slug>/{feature,spec,plan}.md` + `tasks/NN-<slug>.md` into the project repo, reusing the skill-kit materialization engine: SHA-256 hashes, drift detection, 409 on overwriting foreign edits, managed block in `BACKLOG.md` index between markers. Manual file edits surface as drift in the UI with a Re-materialize action; never auto-overwritten.
- `archive` → same markdown written under `~/.overmind/projects/<slug>/backlog/`; free rewrites (internal cache, no drift guards).

### UI (approved 2026-09-08 — full editor)

- New **Planning** sidebar section. Feature board: status, task progress (n/m done), spec/plan validated chips.
- Feature page: tabs Spec / Plan / Tasks / Result. Spec/Plan: markdown editor (xl modal + Write/Preview from Entries, XML-section highlight) with Approve button when the gate is on.
- Tasks: light kanban by status; card opens the large body editor + `<log>` timeline; status moves via buttons (no drag in v1).
- Project settings: `planningMaterialization` (inherit/off/on/archive) + `requireHumanValidation` (unset/on/off). Global defaults for both on the Settings page.

### Migration (approved 2026-09-08)

- One-off importer as a Host CLI flag (`--import-backlog <path>`): parses frontmatter + XML sections of a file-based BACKLOG/ into features/artifacts with statuses and log history. Idempotent by slug. The Overmind repo's own BACKLOG/ (features 001–005) is the test corpus and first dogfood.

## Requirements

- R1. CRUD + version history for features and artifacts; EF migration included.
- R2. Status enums and frozen semantics enforced server-side (HTTP API and MCP share one service path).
- R3. Validation gate behavior per `requireHumanValidation`, including the MCP STOP state.
- R4. Materialization strategies off/on/archive + global default inheritance; drift guards in `on` mode only.
- R5. MCP resources/tools per the contract above; bootstrap prompt updated.
- R6. UI: board, feature tabs, editors, Approve, task status moves, settings toggles.
- R7. Importer converts the repo's own BACKLOG/ losslessly (spot-verified fields: statuses, logs, task order from NN prefixes).
- R8. `dotnet build && dotnet test` green; `npm run lint && npx tsc --noEmit && npm run build` clean.

## Non-goals

- Transition matrices, required markdown sections enforced by schema, cross-feature dependencies, multi-project programs, drag-and-drop, comments, notifications, git commits.
- Replacing Entries — planning artifacts and knowledge entries stay separate families (cross-links are a later decision).

## Open questions

None — all six brainstorm decisions locked (model C, full hierarchy C, DB-truth A with per-project toggle, gates C, full-editor UI B, importer A, materialization off/on/archive default off + global default).
