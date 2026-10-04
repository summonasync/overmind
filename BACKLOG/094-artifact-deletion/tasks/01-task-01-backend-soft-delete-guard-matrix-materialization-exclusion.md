---
feature: 094
task: 01
slug: artifact-deletion
status: review
review: light
created: 2026-10-04
brief: Backend: DeletedAt/DeletedBy soft delete, guard matrix, listings/current_task/materialization exclusion, feature-log audit line, migration + tests
---

# Task 01 — Backend: soft delete + guard matrix + materialization exclusion

<goal>
Add the guarded soft-delete model to the domain and make every read path, current_task selection and materialization pass exclude deleted artifacts.
</goal>

<scope>
- PlanningArtifact: DeletedAt/DeletedBy (source marker) + Delete domain method carrying the guard matrix; body and version history stay in DB.
- Guard matrix (server-side, door-agnostic core): agent door — draft artifacts only (validated/done → 409 naming the reason); human door — unrestricted (validated documents deletable; auto-transition machinery handles the fallout); task with incoming blockedBy/task-dependency edges → 409 «clean up dependencies first».
- All listings/rosters/current_task/materialization exclude deleted artifacts.
- Deletion appends an audit line to the FEATURE's <log> («artifact task #3 deleted by agent, reason») — the feature log outlives the artifact.
- Materialization: tracked-file removed on the next pass; NO tombstone file; clobber-guard unchanged (hand-edited file refuses the pass as any drift).
- Migration + unit/contract tests per AC.
</scope>

<anti-scope>
- MCP/HTTP doors (task 02), web UI (task 03), restore (v1.1), force flag, batch deletion, tombstone files.
</anti-scope>

<constraints>
- AddColumn migration is safe for FTS triggers — the 099 raw-SQL pattern applies only if a rebuild ever appears.
- No force flag in v1: validated-via-human-door is the escape hatch.
</constraints>

<acceptance-criteria>
- [x] Migration AddArtifactDeletion (DeletedAt/DeletedBy) up+down green, migration round-trip pinned.
- [x] Guard matrix pinned: agent + every draft kind → allowed; agent + validated spec/plan/result or done → 409 with reason; blockedBy/task-dependency edges → 409 «clean up dependencies first».
- [x] Human door unrestricted path pinned: deleting a validated spec pulls the feature out of review via auto-transitions.
- [x] Deleted artifacts vanish from listings, roster, current_task and materialization plan (exclusion pinned).
- [x] Feature <log> audit line survives the artifact (artifact gone, line readable).
- [x] Materialization removes the file on the next pass; no tombstone file; hand-edited file still refuses.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (all); plan 0b80c38а (task 1); dependency linkage 030/081; 099 raw-SQL migration pattern (only if rebuild appears).
</refs>

<obstacles>
- Audit line label: spec example reads «artifact task #3 deleted by agent» — implemented as `artifact task 03 deleted by agent` (raw order, D2), matching the codebase-wide task addressing used by blockedBy, aliases and import notes; a 1-based "#3" would have been a second numbering convention.
- Materialization stale-file cleanup did not exist before this task: previously only feature-level ClearAsync removed files, so a hard-deleted artifact already leaked its file. Added a two-phase pass in PlanningMaterializer (guard: drift on an unplanned tracked path refuses the run unless force; exec: hash-verified removal + row drop). Clobber-guard semantics for planned files untouched.
- Migration Down: the generated DropColumn rebuilds the SQLite table via a temp copy, which silently kills the planning FTS triggers (caught by PlanningFtsMigrationTests). Rewrote Down to native `ALTER TABLE ... DROP COLUMN` per the 099 raw-SQL pattern. Up stays AddColumn (safe per constraints).
- FTS raw-SQL search (FtsSearchService.SearchArtifactsAsync) now filters `DeletedAt IS NULL` — otherwise deleted artifacts kept surfacing in fulltext hits while 404-ing on read.
- Re-creating an artifact at a freed order required the unique index (FeatureId, Kind, Order) to become partial (`WHERE "DeletedAt" IS NULL`), otherwise re-import/upsert after a deletion collides with the soft-deleted row.
- Outgoing blockedBy edge rows of the deleted artifact are dropped at delete time (its own dependencies die with it); incoming edges from live dependents block deletion. Invariant: a soft-deleted artifact holds no edges, so future deletions never see stale dependents.
</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
- 2026-10-04 [agent] — taken into work
- 2026-10-04 [agent] — implemented: DeletedAt/DeletedBy + Delete(byHuman) domain guard matrix; agent door draft-only, human unrestricted, dependents → 409; global query filter excludes deleted from all listings/roster/current_task/materializer/FTS; feature-log audit line; materializer stale-file cleanup pass; migration AddArtifactDeletion (partial unique index, native-DROP Down per 099). Gates: dotnet build 0/0; Core 289, Infrastructure 609, Api 585, Mcp 435 — all green; format clean (migration file follows EF-generated repo style). No commits.
- 2026-10-04 [agent] — to review — backend soft delete complete. Scope: PlanningArtifact.Delete(byHuman) with guard matrix (agent draft-only / human unrestricted, 409-class PlanningException ArtifactDeletionRefused + DeletionBlockedByDependents «clean up dependencies first»); OvermindDbContext global query filter (DeletedAt == null) + partial unique index (FeatureId, Kind, Order) WHERE DeletedAt IS NULL; PlanningArtifactService.DeleteAsync reworked (soft delete, dependents guard, outgoing-edge cleanup, auto-transitions: validated spec/plan demote + done-task reopen from review, feature-log audit line with agent/human marker); PlanningMaterializer stale tracked-file cleanup pass (no tombstone, drift refuses unless force); FtsSearchService excludes deleted. HTTP human-door DELETE route kept on byHuman:true; agent MCP/HTTP door = task 02. Migration 20261004112512_AddArtifactDeletion with round-trip pins. Files: src/Overmind.Core/Entities/PlanningArtifact.cs, src/Overmind.Core/Abstractions/PlanningFailureCode.cs, src/Overmind.Core/Abstractions/PlanningException.cs, src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs, src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs, src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs, src/Overmind.Infrastructure/Search/FtsSearchService.cs, src/Overmind.Host/Api/PlanningArtifactsApi.cs, src/Overmind.Infrastructure/Migrations/20261004112512_AddArtifactDeletion.{cs,Designer.cs} + snapshot. Tests: PlanningArtifactTests (guard matrix theory), PlanningArtifactDeletionTests (11 service cases), ArtifactDeletionMigrationTests (4), PlanningMaterializerTests (+3), AgentPlanningApiTests (+1 HTTP exclusion pin), reworked PlanningServiceTests/TaskDependencyServiceTests delete cases. Gates: build 0/0; tests Core 289 / Infrastructure 609 / Api 585 / Mcp 435 green; no commits made.
- 2026-10-04 [agent] — review returned PASS-WITH-NITS (audits/review-094-01.md): 0 blockers, 1 WARNING (unpinned FTS deletedAt filter — one test needed), 3 NIT (dead feature.Touch, unreachable dependents.Count==0 branch, Down() lacks direct column/index asserts); gates re-run by the reviewer green (Core 289 / Infra 609 / Api 585); fix-round dispatched before task 02; observation (human deletion of validated result / done task auto-transitions) handed to task 03 closeout.
</log>