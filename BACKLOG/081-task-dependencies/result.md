---
feature: 081
slug: task-dependencies
status: draft
created: 2026-09-27
---

# Result 081 — Task-level dependencies: blockedBy on task artifacts, honored by current_task (030-семантика на уровень тасок)

<goal>
Delivery record for 081-task-dependencies: 030 feature-dependency semantics extended one level down — `blockedBy` sibling orders on task artifacts (declarative replace on upsert), closure done|review, current_task skip/stop enforcement, additive roster fields, depends_on frontmatter round-trip, web chips + dependencies panel. Eight tasks (01-06, 08 + closeout 07), all reviewed.
</goal>

<outcome>
Shipped end to end across the wave:
1. Core: `PlanningTaskDependency` link table (composite PK, 030 idiom, migration `20260927000000_AddPlanningTaskDependencies`), self-referencing cascade pair on `PlanningArtifacts`.
2. `TaskDependencyService`: order→id resolution at upsert, declarative replace (empty array clears), self/unknown/duplicate order → 400, cycle → 400 naming the chain, closure done|review at read time.
3. current_task: blocked todo/inProgress tasks are skipped in favour of the next actionable one; every-actionable-blocked → stop state naming the chain (task order + blockers), reads and artifact writes stay open.
4. MCP passthrough: `blockedBy` in `planning_upsert_artifact` (task) / `planning_upsert_tasks` items; additive `blocked`/`blockedBy` on roster and artifact responses; host binding on the single upsert door (tail task 08).
5. Materialization: `depends_on:` frontmatter key (blockers-first ordering), byte-stable import round-trip.
6. Web: blocked chips on task cards + dependencies panel on the feature page.
7. Closeout (07): docs anchors (CONTEXT.md Dependencies bullet + SQLite cascade caveat in Storage, STRUCTURE.md write-surface note with frozen-edge metadata exception, SessionBootstrapPrompt DependencyRouting task-level clause) + live walk on an isolated host proving skip/stop/roster/round-trip.

Gates at closeout: dotnet build 0 warnings / 0 errors; full suite 1621/1621 (Core 225, Infrastructure 535, Api 476, Mcp 385 — baseline held); web lint (0 errors, 6 pre-existing warnings) + tsc + build green. Live walk: isolated host (scratch HOME, port 4797, --no-tray, killed by PID; prod 4761 untouched) — batch T1,T2,T3 with T3 blockedBy [1,2]; roster mid-chain shows T3 blocked=true; current → T1 → done → T2 → done → T3 (blocked=false, edges declared); all-blocked variant (W2 status=blocked) → "STOP: every actionable task is blocked by unclosed tasks: task 1 blocked by [2]"; materialized files carry `depends_on: 01, 02` / `depends_on: 02`; re-import → "2 features (0 new), 0 artifacts, 0 skipped", exit 0, edges preserved.
</outcome>

<deviations>
All plan deviations of the wave, kept deliberately:
- Task 03: batch integration — `blockedBy` landed on the batch door (`planning_upsert_tasks` items) alongside the single upsert, not per-task only; pinned by 6 tests.
- Task 02: no shared Core helper — `RequireLogHistoryAppendable` stays a byte-duplicate in `PlanningArtifact` + `PlanningFeature` per contract; extraction deferred until a third copy appears (microbatch #101).
- Task 08 (tail): host binding of `blockedBy` on the single upsert door was discovered as a gap in review 04 and dispatched as a tail task instead of stretching task 04; review 08 also closed the carried McpException guard and fixture-live-semantics addenda from rev-04.
- Docs (07, deliberate semantics): task-edge edits are metadata-level — a `blockedBy` re-upsert needs no change note even on a validated (frozen) task; edges stage outside the body path. Recorded in STRUCTURE.md with microbatch #109 as the guard candidate.
- SQLite-only topology: the self-referencing cascade pair (`BlockedById`/`BlockingId` both Cascade) works on SQLite; SQL Server would reject the multiply-defined cascade path — one-line caveat recorded in CONTEXT.md Storage.
</deviations>

<deferred>
- Post-deploy live web check: blocked chips payload rendering, task-card chips, feature-page dependencies panel, refetch behavior after edge edits — code-complete and gate-verified, browser pass rides the next deploy.
</deferred>

<follow-ups>
Microbatch queue, waves 083/081 (2026-09-27/28), items #100-117:
- 083 wave: #100 Version-pin test for note-only update; #101 RequireLogHistoryAppendable dup → shared helper on third copy; #102 priority numeric-string acceptance asymmetry; #103 Enum.Parse → TryParse in AgentPlanningApi; #104/#108 whitespace-logNote test pins; #105 PlanningFeatureService split candidate (553 lines); #106 wire-pin for param descriptions; #107 mutation echo shape unification.
- 081 wave: #109 frozen-edge guard candidate (logNote on validated edge change if product wants it); #110 test naming swings in PlanningServiceTests; #111 tracked:false on UnclosedBlockersAsync/BlockedOrdersAsync read paths; #112 N+1 in current_task stop path (UnclosedChainAsync, 2 queries per candidate); #113 BlockedStatesAsync re-reads tracked tasks; #114 batch-door non-array blockedBy silent drop — partially closed by 081/08 McpException guard, re-verify remaining paths; #115 raw GetRawText forward gives faceless 400 instead of McpException on non-int item elements; #116 import-level cycle branch (TaskDependencyService) uncovered by test; #117 depends_on D2 sort unpinned by unsorted input.
</follow-ups>

<refs>
audits/review-081-01..08.md (per-task verdicts; all light reviews PASS/PASS-WITH-NITS); spec 081 validated (user decisions: edges = upsert field, closure = done|review, full-stack incl. web); plan ca4721f1; 030 result (feature-level precedent); microbatch queue #100-117.
</refs>

<log>
- 2026-09-28 [agent] — result drafted at closeout (task 07): docs anchors grep-verified, live walk protocol fully proven on the isolated host, all gates green (build 0W/0E, suite 1621/1621, web lint/tsc/build).
</log>