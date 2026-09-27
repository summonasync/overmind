---
feature: 081
task: 03
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: current_task skips blocked (all-blocked stop names chain); roster/full-read blocked+blockedBy task-only
---

# 081/03 — current_task skipping + roster blocked fields

<goal>
The machine gate: planning_current_task skips blocked tasks (stop names the chain when all are blocked), and task payloads expose blocked/blockedBy so agents see what is actionable.
</goal>

<context>
Executor reads ONLY this file. Prerequisites: tasks 01-02 done. 030 LESSON: the current-task resolver lives in src/Overmind.Host/Api/AgentPlanningApi.cs Current handler (NOT Infrastructure) — add skipping minimally there using the task-02 BlockedOrdersAsync query. Stop/empty state shapes: existing current_task stop formats (feature-blocked STOP from 030, empty-board states) — follow byte-shape conventions. Roster: src/Overmind.Host/Api/PlanningFeaturesApi.ListArtifacts + PlanningMapper.ToRosterItem (080). Full feature read payload: PlanningFeatureResponse artifacts.
</context>

<scope>
- AgentPlanningApi.Current: after feature-gate checks, iterate candidate tasks lowest-order-first; skip tasks with unclosed edges; if candidates exist return the first actionable; if the feature has tasks but ALL are blocked → stop state naming the chain (task order + blocker orders, same shape conventions as the 030 feature-blocked STOP); if no tasks at all → existing empty state unchanged.
- Roster (ListArtifacts) + full feature read: task items gain additive blocked (bool) + blockedBy (sorted sibling orders) — task-only fields exactly like review/acTicked; documents never carry them.
- Update canned contract pins: roster shape (070 canned / 080 pins), current_task canned shapes — additive only.
- Tests (Api.Tests): skip picks the next actionable (3-task chain), all-blocked stop names the chain, mixed done/review closure picks correctly, roster task-only fields, documents clean, unblocked feature payload byte-identical to pre-081 for tasks without edges.
</scope>

<anti-scope>
- No Mcp changes (task 04), no web (task 06), no materialization (task 05). Human door behavior unchanged (reads may show new fields; no gating).
</anti-scope>

<constraints>
- No comments; English; no new packages; NO git commits.
- dotnet build 0W/0E; full suite green (baseline + tasks 01-02).
- Diff: Host Api + contracts/mapper + Api.Tests.
</constraints>

<acceptance-criteria>
- [x] current_task skips blocked tasks (lowest-order-first), returns first actionable.
- [x] All-tasks-blocked → stop state naming the chain (order + blockers); empty feature → unchanged state.
- [x] Roster + full read task items carry blocked/blockedBy (task-only); canned pins updated additively.
- [x] Unblocked feature behaves identically to pre-081 (payload for edge-less tasks unchanged).
- [x] dotnet build 0W/0E + full suite green.
</acceptance-criteria>

<refs>
- AgentPlanningApi.cs Current handler (resolver — 030 lesson)
- PlanningFeaturesApi.ListArtifacts + PlanningMapper.ToRosterItem (080)
- 030 stop-state shapes (feature-blocked STOP text)
- TaskDependencyService from task 02
</refs>

<obstacles>
<obstacles>
- 2026-09-27 [coder] SCOPE EXTENSION (sanctioned by orchestrator, plan deviation): coder 081/02 did not touch the batch door — PlanningArtifactService.UpsertTasksAsync did not accept blockedBy on item contracts, while the spec promises blockedBy on both upsert surfaces and MCP task 04 forwards the field to the batch. Added in this task: PlanningTaskUpsert.BlockedBy (optional, null = untouched, [] = clear), Host PlanningTaskUpsertRequest.BlockedBy + CreateTasks forwarding, and batch edge staging via TaskDependencyService inside the existing batch SaveChanges. Mechanics: two-phase staging (all bodies first, then edges per item) so a batch may reference tasks created later in the same batch; siblings load gained .Include(BlockedBy); failures wrap as TaskBatchItem(index) and persist nothing (single SaveChanges). Tests: Infra (edges in one save, forward reference within batch, unknown order rolls back the whole batch) + Api (batch creates edges and echoes them, unknown order 400 names tasks[1] and writes nothing, cycle across items 409 naming tasks[2] and the path).
- 2026-09-27 [coder] Build gate: task-02 test file carried a pre-existing CS4014 warning (unawaited NSubstitute DidNotReceiveWithAnyArgs().SyncAsync in TaskDependencyServiceTests.ReplaceEdgesAsync_UnchangedSet_DoesNotSyncMaterialization) that broke the 0W/0E gate; fixed minimally by awaiting the assertion call. dotnet format --verify-no-changes reports issues only in EF-generated migration files (generator-owned style, incl. task 01's migration) — none in files touched here.
- 2026-09-27 [coder] Design note: blocked/blockedBy serialize only when the task has at least one edge (documents and edge-less tasks omit both) — this is what makes the byte-identical pre-081 AC hold; a task with closed edges still reports blocked=false + blockedBy=[orders]. Roster/full-read/current/batch-echo all pass a TaskBlockedState lookup keyed by order; blocked flag mirrors TaskDependencyService closure semantics (done|review closes).
</obstacles>
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch after task 02.
- 2026-09-27 [agent] — implemented: current_task skips edge-blocked tasks (lowest-order-first, AgentPlanningApi.Current + TaskDependencyService.BlockedOrdersAsync), all-blocked stop names the chain per 030 stop shape; roster ListArtifacts + full feature read + current/batch echo carry task-only blocked/blockedBy (omitted for docs and edge-less tasks — byte-identical pre-081); SCOPE EXTENSION per orchestrator: batch door UpsertTasksAsync + PlanningTaskUpsertRequest accept optional per-item blockedBy (two-phase staging, TaskBatchItem wrapping, nothing persists on failure); fixed pre-existing CS4014 in task-02 test to hold the 0W gate. Gates: build 0W/0E, full suite 1601/1601 green (baseline 1590 + 11 new). Status → review.
- 2026-09-27 [agent] — coder done → review: 5/5 AC ticked, build 0W/0E, suite 1601 green (11 new tests: 8 Api + 3 Infra); batch-door extension documented in obstacles. No commits made.
- 2026-09-27 [agent] — light review PASS-WITH-NITS (2 NIT microbatch; carried WARNING frozen-gate → решение в task 07) — audits/review-081-03.md; независимые гейты: build 0W/0E, 1601/1601; edge-less фича байт-идентична pre-081 (старые 070/080 пины без правок); батч-девиация запина 6 тестами; done by orchestrator after review
</log>