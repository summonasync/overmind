---
feature: 081
task: 02
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: TaskDependencyService: order-resolve, DFS cycles, closure done|review; blockedBy wired into upsert
---

# 081/02 — TaskDependencyService + upsert blockedBy resolution

<goal>
The dependency engine: order-based edge resolution with cycle rejection, closure queries, and the declarative blockedBy field wired into the artifact upsert path.
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 01 (entity/migration) done. Templates: src/Overmind.Infrastructure/Planning/FeatureDependencyService.cs (030: DFS cycles with chain in error, OpenBlockersAsync closure = blocker done|review) and the artifact upsert path in PlanningArtifactService.UpsertAsync (transaction, sync). Feature resolution pattern: PlanningFeatureService.
</context>

<scope>
- New src/Overmind.Infrastructure/Planning/TaskDependencyService.cs: ReplaceEdgesAsync(taskArtifactId, siblingOrderIds resolved within the feature) — declarative replace (empty clears); resolution order→artifact id with 400s: unknown order (names the order), self-reference, duplicate; DFS cycle detection (direct + transitive, error carries the chain as order path); UnclosedBlockersAsync(taskId) — blocker status ∈ {Done, Review} closes; BlockedOrdersAsync(featureId) — per-task blocked flag + blocker orders for listings/resolver.
- PlanningArtifactService.UpsertAsync task path: optional blockedBy (list of sibling orders) on the create/update request contracts — applied inside the existing transaction after the artifact row exists; absent field = keep existing edges (update) / none (create); explicit empty array = clear. DI registration for the new service.
- Human-door PUT artifacts is NOT given the field (agent-surface field per spec; human door unchanged).
- Tests (Infrastructure.Tests): direct cycle, transitive cycle (chain in error), unknown order, self-ref, replace/clear semantics, closure open→close on blocker Done and Review, re-block when blocker rolls back to in_progress, cascade on delete, absent-field keeps edges.
</scope>

<anti-scope>
- No Host handlers, no Mcp, no web, no materialization (later tasks). No human-door field.
</anti-scope>

<constraints>
- No comments; English; no new packages; NO git commits.
- dotnet build 0W/0E; full suite green (baseline + task 01).
- Diff: Infrastructure Planning + Infrastructure.Tests (+ request contract records if they live in Infrastructure contracts — otherwise flag in obstacles for task 03).
</constraints>

<acceptance-criteria>
- [x] blockedBy (orders) resolves to edges on task upsert; unknown order/self-ref/duplicate → 400 with specific reason.
- [x] Cycles (direct + transitive) rejected with the order-path chain in the error.
- [x] Replace-semantics: re-upsert replaces edge set; empty array clears; absent field preserves.
- [x] UnclosedBlockers/BlockedOrders: closure = blocker Done|Review; re-block on rollback to in_progress.
- [x] Cascade on task delete; dotnet build 0W/0E + full suite green.
</acceptance-criteria>

<refs>
- src/Overmind.Infrastructure/Planning/FeatureDependencyService.cs (030 template)
- PlanningArtifactService.UpsertAsync (transaction + sync path)
- PlanningException failure kinds (400 vocabulary; add a new code only if no fit — follow 030 naming)
</refs>

<obstacles>
- Wire field NOT added here: CreatePlanningArtifactRequest/UpdatePlanningArtifactRequest live in Overmind.Host.Api.Contracts (Host = anti-scope). The engine surface is the optional `blockedBy: IReadOnlyList<int>?` parameter on PlanningArtifactService.UpsertAsync — task 03 (agent door) and task 04 (Mcp) must forward it there. Human-door PUT stays field-free per scope.
- planning_upsert_tasks batch items do NOT carry blockedBy yet — UpsertTasksAsync untouched, task 04 wiring (per plan split).
- Core touched beyond the declared Infrastructure-only diff: PlanningArtifact.ReplaceBlockedEdges + PlanningException factories (TaskDependencyCycle → RuleViolation/409; BlockedByOrderUnknown, BlockedBySelfReference, BlockedByDuplicateOrder, BlockedByNotAllowedForKind → InvalidInput/400) + 4 PlanningFailureCode entries. Reason: task 01 shipped entity+EF only and its plan line promised the edge-replace helper; Infrastructure cannot construct PlanningTaskDependency (internal Create, InternalsVisibleTo covers only Infrastructure.Tests).
- UpsertAsync unique-violation retry path now detaches staged edge rows (PlanningTaskDependencies.RemoveRange(created.BlockedBy)) and re-stages against the reassigned order — auto-order retry with blockedBy stays consistent.
- blockedBy staging happens BEFORE the upsert SaveChanges (single flush): contract errors (400/cycle) refuse the whole upsert with zero writes, verified by UpsertArtifact_TaskBlockedByUnknownOrder_RefusesWithoutCreating.
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch after task 01.
- 2026-09-27 [agent] — implemented: TaskDependencyService (ReplaceEdgesAsync, StageBlockedByAsync, UnclosedBlockersAsync, BlockedOrdersAsync) + blockedBy staged pre-flush in PlanningArtifactService.UpsertAsync (atomic refusal on contract errors, retry-path re-stage); build 0W/0E, suite 1590 green (baseline 1566 + 24 new)
- 2026-09-27 [agent] — agent door: 5/5 AC ticked — engine + upsert wiring done, build 0W/0E, full suite 1590/1590; moved to review for the reviewer
- 2026-09-27 [agent] — light review PASS-WITH-NITS (1 WARNING + 2 NIT + 2 OBS) — audits/review-081-02.md; независимые гейты: build 0W/0E, 1590/1590; WARNING frozen-gate (рёбра validated-таска меняются без logNote) решён оркестратором: docs-оговорка в task 07 (030-прецедент «рёбра = метаданные») + microbatch #109 на гард-кандидата; батч-пробел признан плановой поэтапностью (закроют 03/04); done by orchestrator after review
</log>