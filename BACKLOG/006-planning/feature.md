---
feature: 006
slug: planning
status: ready
version: 1
created: 2026-09-08
---

# Feature 006 — Planning: the ceremony as a first-class entity

# Feature 006 — Planning: the ceremony as a first-class entity

<goal>
The development ceremony — feature → spec → plan → task → result — becomes a first-class citizen of Overmind. Agents create and drive planning artifacts through MCP; humans review, approve, and edit them in the UI. The database is the source of truth; markdown materialization to disk is a per-project choice (repo `BACKLOG/`, local archive in `~/.overmind/`, or none). This replaces the file-only BACKLOG/ workflow with a model that protects the two invariants that matter — status enums and frozen validated snapshots — while keeping all content as agent-native markdown with XML sections.
</goal>

<requirements>
- Full hierarchy per project: PlanningFeature → PlanningArtifact (Spec / Plan / Task / Result) with markdown+XML bodies and versioned content.
- Task statuses as an enum (todo / in_progress / review / done / blocked); explicit numeric task order within a feature.
- Spec/Plan lifecycle: draft → validated (frozen); editing a validated artifact requires a dated change note and resets it to draft.
- Validation gate, two-level: global default (off) + per-project override (unset = use global / on / off). When effective-on, only the UI (human) can validate spec/plan; agents get a clear STOP signal through MCP.
- MCP: planning resources (feature board, full feature, current task) and tools (create feature, upsert artifact, set status, validate, append log).
- Materialization per project: `off` (default) / `on` (repo `BACKLOG/` with drift guards) / `archive` (`~/.overmind/`, free rewrite), plus a global default strategy in Settings that projects inherit.
- UI: full ceremony editor — feature board, spec/plan markdown editors with Approve, task board with status moves and log timeline, project workflow settings.
- One-off importer: parses an existing file-based BACKLOG/ into the database (idempotent by slug).
</requirements>

<non-goals>
- Rigid workflow enforcement (transition matrices, required fields beyond the two invariants) — structure lives in markdown conventions, not the schema.
- Cross-feature dependencies / multi-project programs in v1.
- Drag-and-drop task board, notifications, comments/discussion threads.
- Git integration beyond writing markdown files to disk (no auto-commits ever).
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
-->
