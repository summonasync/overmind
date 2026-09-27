---
feature: 081
task: 07
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: Docs anchors + bootstrap line + live MCP walk + result draft
---

# 081/07 — docs anchors + live walk + closeout

<goal>
The closeout: doc anchors learn task dependencies, the live MCP walk proves the chain end-to-end, result artifact drafted.
</goal>

<context>
Executor reads ONLY this file. Prerequisites: tasks 01-06 done. Anchor locations: CONTEXT.md planning bullet area (030 has a dependency sentence), STRUCTURE.md (write-surface / dependency note), SessionBootstrapPrompt dependency-routing line (030 added it — extend with the task-level clause). Live walk pattern: isolated host (scratch HOME + DOTNET_CLI_HOME, Overmind__BindUrl port 479x, kill by PID — NOT pkill; prod 4761 untouched).
</context>

<scope>
- CONTEXT.md: one sentence in the planning dependencies context — task-level blockedBy (order-based, same-feature), closure done|review, honored by current_task.
- STRUCTURE.md: write-surface note — blockedBy rides artifact upsert (declarative replace), edges close on blocker done|review.
- SessionBootstrapPrompt: dependency-routing line gains the task-level clause (030 line location).
- Live walk on the isolated host (protocol in the task log): feature with T1,T2,T3 where T3 blockedBy [1,2] → current_task returns T1 → T1 done → returns T2 → T2 done → returns T3 → roster shows blocked flags mid-chain → all-blocked variant stops naming the chain → depends_on in materialized file → re-import round-trip.
- Result artifact for the feature (draft) summarizing outcomes/deviations/follow-ups.
</scope>

<anti-scope>
- No code changes; docs + walk + result only.
</anti-scope>

<constraints>
- English; NO git commits; isolated host only (scratch HOME, kill by PID).
- dotnet build + full suite green; web gates green.
</constraints>

<acceptance-criteria>
- [x] CONTEXT + STRUCTURE + bootstrap lines present (grep-verified).
- [x] Live walk protocol executed and logged: skip/stop/roster/round-trip all proven.
- [x] Result artifact drafted on the board.
- [x] All gates green (build, full suite, web).
</acceptance-criteria>

<refs>
- CONTEXT.md / STRUCTURE.md / SessionBootstrapPrompt.cs (030 dependency lines)
- 030 result.md live-walk protocol
- Tasks 01-06 artifacts
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; final task, dispatch after all others.
- 2026-09-27 [agent] — [agent] Executor note from review 081/01: docs sweep should carry one line about the SQLite-only cascade caveat — self-referencing cascade pair (PlanningArtifacts → PlanningTaskDependencies, both Cascade) works on SQLite, SQL Server would reject the topology; CONTEXT/STRUCTURE sweep in this task is the right place (review-081-01 OBSERVATION).
- 2026-09-27 [agent] — [agent] Executor note from review 081/02 (docs sweep addition #2): STRUCTURE write-surface note should state explicitly that task-edge edits (blockedBy re-upsert) are metadata-level and do NOT require a change note even on validated (frozen) tasks — deliberate deviation from the body-edit note rule (030 precedent: features have no freeze; edges are artifact metadata), with microbatch #109 as the guard candidate if product wants notes on frozen edge changes (review-081-02 WARNING).
- 2026-09-27 [agent] — Docs sweep done: CONTEXT.md gained the Dependencies bullet (task-level blockedBy, order-based same-feature, closure done|review, honored by current_task) after the Model bullet, plus the SQLite-only cascade caveat sentence in the Storage section (PlanningTaskDependencies self-referencing cascade pair — SQLite ok, SQL Server would need Restrict); STRUCTURE.md gained the write-surface note (blockedBy declarative replace on upsert, closure done|review, frozen-edge metadata exception — no change note needed on validated tasks, microbatch #109 guard candidate); SessionBootstrapPrompt.cs DependencyRouting extended with the task-level clause. Both review executor notes carried. dotnet build running next, then walk.
- 2026-09-27 [agent] — Live walk executed on isolated host (scratch HOME mktemp, DOTNET_CLI_HOME redirected, Overmind__BindUrl=http://127.0.0.1:4797, --no-tray, PID 686182 killed by PID only; prod 4761 untouched, prod mirror has zero walk features). Protocol over the agent door HTTP API: project walk081 (DiskPath=scratch/repo), workflow materialization=on; feature walk-deps with batch T1,T2,T3 (T3 blockedBy [1,2]) — batch echo: T3 blocked=true blockedBy=[1,2]. Roster mid-chain: T3 blocked=true, T1/T2 null. current -> task order 1. T1 done -> current -> order 2 (T3 skipped, blocked). T2 done -> current -> order 3, blocked=false, blockedBy=[1,2] declared (closure via done). All-blocked variant: feature walk-all-blocked, W1 blockedBy [2], W2 set status=blocked -> current -> state=stop, reason "STOP: every actionable task is blocked by unclosed tasks: task 1 blocked by [2]. Reads stay open; task issuance stops until every blocker closes (done or review)". Materialize (auto-pass on upsert + manual pass: all skipped as content-already-matches): BACKLOG/walk-deps/tasks/03-t3.md carries "depends_on: 01, 02", walk-all-blocked/tasks/01-w1.md carries "depends_on: 02". Re-import round-trip: --import-backlog <scratch repo> -> "2 features (0 new), 0 artifacts, 0 skipped", exit 0, roster post-import still shows T3 blockedBy=[1,2] — byte-stable, edges preserved. Observation: roster endpoint (080 metadata-only) returns no per-artifact id — status moves in the walk used ids from the batch echo.
- 2026-09-27 [agent] — Executor hand-off: docs sweep landed (CONTEXT.md Dependencies bullet + SQLite cascade caveat; STRUCTURE.md write-surface note with frozen-edge metadata exception; SessionBootstrapPrompt DependencyRouting task-level clause — grep-verified), live walk fully proven on isolated host (skip/stop/roster/round-trip; log above), result artifact 9394fdcd drafted (outcome/gates/deviations/deferred/follow-ups #100-117), all 4 ACs ticked, gates green (build 0W/0E, suite 1621/1621, web lint 0E/6 pre-existing warnings + tsc + build). Moving to review.
- 2026-09-27 [agent] — light review PASS-WITH-NITS (0 BLOCKER; 1 WARNING process — audits/review-081-06.md отсутствовал, МАТЕРИАЛИЗОВАН оркестратором из транскрипта ревьюера при закрытии; NIT-1 русский фрагмент H1 — унаследованный тайтл фичи, борд-сторона) — audits/review-081-07.md; независимые гейты: build 0W/0E, 1621/1621; walk-протокол изоляция и полнота подтверждены; done by orchestrator after review
</log>