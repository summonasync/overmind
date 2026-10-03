---
feature: 094
slug: artifact-deletion
status: draft
created: 2026-10-03
---

# Plan 094 — Artifact deletion: human door (HTTP+UI) + agent door (MCP) для сноса артефактов — дубли/мусор сейчас нечистимы

> Pre-flown without the user (same note as the spec): 3 tasks, 01 → 02 → 03 (closeout folds the architect pass in — small wave).

## Task chain

1. **01 — Backend: soft delete + guards + materialization.** PlanningArtifact: DeletedAt/DeletedBy + Delete(domain method) with the guard matrix (draft-only for agents, unrestricted for humans via a flag; blockedBy edges → 409; validated/done → 409 for agents). All listings/rosters/current_task/materialization exclude deleted. Deletion appends the audit line to the feature `<log>`. Materialized-file cleanup on the next pass. Contract tests: guard matrix rows, exclusion from every listing, audit line, feature-log survives.
2. **02 — Both doors: MCP + HTTP.** planning_delete_artifact(projectId, artifactId|alias, source, logNote) — draft-only guards, description documents the matrix; HTTP DELETE (human door, unrestricted) + tests for both doors (guards 409s, alias resolution, audit line, human-deletes-validated path).
3. **03 — Web UI + closeout.** Roster row overflow / artifact detail delete affordance with confirm (lists what gets removed); hidden-from-listings after delete. Closeout: consistency pass, architect light (orchestrator dispatches), result with a post-release click-check item (delete a duplicate task via MCP, verify mirror cleanup).

## Verification

- Guard matrix unit tests (01); door tests (02); gates each task; architect light (03).

<log>
- 2026-10-04 [agent] — plan pre-flown: 3 tasks linear, closeout folds the architect.
</log>