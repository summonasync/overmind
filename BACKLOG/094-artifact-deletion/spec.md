---
feature: 094
slug: artifact-deletion
status: draft
created: 2026-10-03
---

# Spec 094 — Artifact deletion: human door (HTTP+UI) + agent door (MCP) для сноса артефактов — дубли/мусор сейчас нечистимы

> NOTE: drafted 2026-10-04 WITHOUT the Socratic pass — the user is away and ordered spec+plan to be pre-flown. Decisions below are conservative defaults; challenge any of them on return — spec is draft, cheap to amend.

## Goal

Artifacts (spec/plan/task/result) cannot be deleted by any door — no MCP tool, no HTTP route, no UI affordance. Live case (2026-09-28): a duplicate task created by upsert-without-order could only be tombstoned manually. This feature adds guarded deletion to both doors.

## Decisions (conservative defaults, user away — challenge on return)

- **Soft delete.** `DeletedAt` + `DeletedBy` (source marker) on PlanningArtifact; body and version history stay in the DB (audit intact). Deleted artifacts vanish from: all listings/rosters, current_task selection, feature-page roster, and materialization. Restore is a v1.1 follow-up (the data survives; no restore UI in v1).
- **Guard matrix (server-side, both doors):**
  - agent door: DRAFT artifacts only — draft tasks primarily, also non-validated spec/plan drafts; anything validated (spec/plan/result) or done → 409 naming the reason.
  - human door: unrestricted — validated documents CAN be deleted by the human (auto-transitions handle the fallout: deleting a validated spec pulls the feature out of review, per the established invariant machinery).
  - Task with incoming blockedBy/task-dependency edges → 409 «clean up dependencies first» (030/081 linkage); deletion does not silently orphan dependents.
  - No force flag in v1 — validated-via-human-door is the escape hatch; less surface.
- **Audit trail survives the artifact:** the deletion appends a line to the FEATURE's `<log>` («artifact task #3 deleted by agent, reason») — the feature log outlives the artifact. Version history stays in the DB.
- **Materialization:** the materialized file is removed on the next pass (tracked-file cleanup); NO tombstone file — the board is the truth and the mirror equals it (051's tombstone-file pattern is for entries deleted outside the board; artifacts are deleted ON the board). Clobber-guard still applies: a hand-edited file refuses the pass as usual — the operator force-passes as with any drift.
- **Surfaces:** agent door = MCP `planning_delete_artifact(projectId, artifactId | alias, source, logNote)` (alias per 090, draft-only per guards); human door = HTTP DELETE + UI affordance (roster row overflow / artifact detail, confirm step showing what will be removed).

## Non-goals

- Restore UI (v1.1 — data survives), force flag, deleting features via this path (feature deletion exists), tombstone files for artifacts, batch deletion.

<log>
- 2026-10-04 [agent] — spec pre-flown without Socratic (user away, ordered spec+plan): soft delete, guard matrix draft-only-for-agent/unrestricted-for-human, feature-log audit line, file removal without tombstone, no force flag. All decisions marked challengeable.
</log>