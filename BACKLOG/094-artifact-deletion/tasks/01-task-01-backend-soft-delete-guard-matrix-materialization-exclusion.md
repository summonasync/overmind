---
feature: 094
task: 01
slug: artifact-deletion
status: inprogress
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
- [ ] Migration AddArtifactDeletion (DeletedAt/DeletedBy) up+down green, migration round-trip pinned.
- [ ] Guard matrix pinned: agent + every draft kind → allowed; agent + validated spec/plan/result or done → 409 with reason; blockedBy/task-dependency edges → 409 «clean up dependencies first».
- [ ] Human door unrestricted path pinned: deleting a validated spec pulls the feature out of review via auto-transitions.
- [ ] Deleted artifacts vanish from listings, roster, current_task and materialization plan (exclusion pinned).
- [ ] Feature <log> audit line survives the artifact (artifact gone, line readable).
- [ ] Materialization removes the file on the next pass; no tombstone file; hand-edited file still refuses.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (all); plan 0b80c38а (task 1); dependency linkage 030/081; 099 raw-SQL migration pattern (only if rebuild appears).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
- 2026-10-04 [agent] — taken into work
</log>