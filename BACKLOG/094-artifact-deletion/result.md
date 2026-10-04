---
feature: 094
slug: artifact-deletion
status: draft
created: 2026-10-04
---

# Result 094 — Artifact deletion: human door (HTTP+UI) + agent door (MCP) для сноса артефактов — дубли/мусор сейчас нечистимы

## Outcome

All three tasks landed. Artifacts are deletable through both doors behind a single domain guard: soft delete (DeletedAt/DeletedBy — body and versions stay in the DB), the guard matrix lives ONCE in `PlanningArtifact.Delete(byHuman)` — agents delete draft artifacts only (validated/done → 409 naming the reason), the human door is unrestricted (validated documents deletable; auto-transition machinery pulls review features back when a validated spec/plan dies), incoming dependency edges refuse with «Clean up dependencies first» (naming the dependent tasks). No force flag in v1.

Per task:

- **01 Backend** — DeletedAt/DeletedBy + `Delete(byHuman)` + `IsAgentDeletable` (documents draft-only, tasks any non-done); global query filter `DeletedAt == null` excludes deleted from every EF path (listings, roster, current_task, materializer, dependency views); FTS raw-SQL predicate pinned by a post-delete union-search fact test; audit line appended to the FEATURE's `<log>` (outlives the artifact); materializer gained a stale-cleanup pass — unplanned tracked files of the feature are removed on the next pass, no tombstone files (board = truth), clobber-guard intact; migration AddArtifactDeletion with a partial unique index `(FeatureId, Kind, Order) WHERE DeletedAt IS NULL` (freed orders reusable); Down uses raw DROP COLUMN per the 099 FTS-trigger pattern, directly asserted.
- **02 Doors** — MCP `planning_delete_artifact` (artifactId | alias via 090 addressing; description documents the matrix verbatim and is pinned); agent route `DELETE /api/agent/planning/artifacts/{id}` (LockToClaim, byHuman: false) echoing `{id, kind, featureLogLine}`; human `DELETE /api/planning/artifacts/{id}` takes an optional body (logNote/source), still 204-compatible bodyless; the byHuman marker is derived from the door literal — an agent cannot forge «deleted by human».
- **03 Web + closeout** — delete affordances in two places (trash on the task-board card, Delete in the document panel), confirm dialog listing consequences (artifact + feature log line go immediately; versions leave view but stay in the DB; the materialized file is removed on the next pass), warnings for dependency-blocked targets (mirrors the 409 text) and validated artifacts («deletion is permanent»); pessimistic mutation (irreversible), invalidation over `planningKeys.lists()` + feature detail; 409 detail surfaces inside the dialog and does not close it.

## Authorized deviations / decisions

- Query filter instead of per-query exclusion (task 01): one predicate covers every EF path by construction; `IgnoreQueryFilters` grep is clean in src.
- Partial unique index instead of re-shuffling orders on delete (task 01): freed orders become reusable without a rewrite pass.
- Human DELETE gained an optional body (task 02): minimal API does not infer DELETE bodies — `[FromBody]` is required for the route to even build (recorded in obstacles; caught only at test run).
- Web warnings computed locally in `lib/artifact-deletion.ts` (task 03): the preview renders before any request; texts mirror the backend 409 prefix byte-for-byte.

## Verification evidence

Per-task reviews: 01 — PASS-WITH-NITS (audits/review-094-01.md), fix round closed all 4 findings; 02 — PASS-WITH-NITS (audits/review-094-02.md), fix round closed the WARNING (delete_artifact added to the 099 lock matrix) and pinned the source passthrough (the bodyless-204 pin already existed from the 006 era — the with-body form pinned additionally); 03 web — PASS (audits/review-094-03-web.md), 0 findings of weight.

Architect light on the final tree (audits/architect-review-2026-10-04-094.md): **APPROVED-WITH-WARNINGS, 0 blockers**; W1 (stale CONTEXT.md tool roster — 26 tools, missing claim/unclaim and delete; two-doors paragraph unaware of the agent DELETE) fixed in this closeout; N1–N3 (dead Touch in AppendLogAsync, unindexed DependentOrdersAsync probe, STRUCTURE ceiling line) moved to the microbatch ledger (#168–170).

Gates on the final tree, independently re-run by the architect: dotnet build 0/0; full suite SEQUENTIAL — Core 289/0, Infrastructure 611/0, Api 595/0, Mcp 442/0 = **1937/0**; web — eslint 0 errors (6 pre-existing), tsc clean, node tests 71/0.

## Post-release click-check list

1. **MCP duplicate cleanup** — upsert a task without order (the 090-class duplicate), delete it via `planning_delete_artifact` with a reason; verify the echo carries the feature audit line and the mirror loses the file on the next pass.
2. **Human UI delete** — delete a draft task from the task board; confirm dialog consequences; row disappears; BACKLOG file gone after the next materialize.
3. **Validated document via UI** — delete a validated spec on a review feature with the UI; the feature pulls back to inProgress ([auto] line), mirror file removed.
4. **Guard UX** — try deleting a task with dependents (agent door): 409 «Clean up dependencies first: task 02»; the web dialog shows the same warning before any request.
5. **Stale-file cleanup** — hand-create a stray file at a planned task path, delete the task on the board, run materialize: stray file refused (clobber-guard), force removes it with a warning.

## Follow-ups (microbatch queue)

- N1 dead `feature.Touch()` in `AppendLogAsync` (#168), N2 unindexed `DependentOrdersAsync` probe (#169), N3 STRUCTURE ceiling line (#170).
- Restore UI (v1.1 — data survives), batch deletion, per the spec non-goals.
- Web suggestion: close-guard on Escape during pending (v1.1 polish, mirrors delete-feature dialog).

## Status

Feature ready for user validation; result stays draft until then. Post-release click-check rides the batch release.

<log>
- 2026-10-04 [agent] — drafted by the orchestrator at the wave close: outcome per task, deviations (query filter, partial index, DELETE body, local preview), verification evidence (3 reviews + architect APPROVED-WITH-WARNINGS, 1937/0 + 71/0 sequential), post-release click-check (5 scenarios).
- 2026-10-04 [agent] — architect closeout: W1 CONTEXT.md roster fixed (26 tools, claim/unclaim + delete documented, two-doors paragraph updated), N1–N3 to the microbatch ledger.
</log>