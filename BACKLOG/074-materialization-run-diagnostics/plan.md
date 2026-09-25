---
feature: 074
slug: materialization-run-diagnostics
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Plan 074 — Materialization run diagnostics: lastSuccess/lastAttempt, per-file outcome, atomicity, diagnostic tool

<approach>
Two layers, sequential (shared files: Project entity, materializer report types): (1) storage + engine — Project fields, migration, plan-then-write atomicity, report persistence, Infrastructure tests (the atomic-abort test is the load-bearing one); (2) surfaces — onboard/materialize render sites split the fields, planning_materialize_status tool with stalePaths over 067 tracking. Single dispatch per task; both light review.
</approach>

<tasks>
- [ ] 01-atomic-run-persistence — Project: +LastMaterializationAttemptAt (every run), +LastMaterializationRun TEXT (JSON report {startedAt, finishedAt, atomic, written[], skipped[{path,reason}], refused[{path,reason}]}); additive migration; PlanningMaterializer.SyncAsync plan-then-write: full op plan before any disk write, any refusal aborts the whole run (zero writes, atomic=false, refusals recorded); skip stays non-fatal; success stamps LastMaterializedAt (unchanged name, now lastSuccess semantics) AND AttemptAt; failed run stamps AttemptAt + error fields (054 behavior kept); report persisted on every completed/aborted run; Infrastructure tests: atomic abort byte-identical tree, skip-only success, attempt stamped on failure, report round-trip, 054 compat → tasks/01-atomic-run-persistence.md. Verify: dotnet build 0 warnings; dotnet test Overmind.sln (baseline 1422)
- [ ] 02-echo-and-status-tool — Host render sites (042 onboard block + planning_materialize response, 054 shape): "lastSuccess / lastAttempt" split replacing the single lastRun prose (attempt reason stays when attempt ≠ success); Api.Tests both sites; MCP planning_materialize_status(projectId) — ReadOnly+Idempotent, agent door: lastSuccess/lastAttempt, persisted per-file report, tracked count (067), stalePaths (tracked minus deliberately-written/skipped, drift-aware), strategy/targetDir — compact structured echo (037), no bodies; Mcp.Tests wiring + canned fixture; tool description states the mirror-diagnostic contract → tasks/02-echo-and-status-tool.md. Verify: dotnet test Overmind.sln. Depends on 01.
</tasks>

<verification>
- Live: force a refusal (pre-edit a materialized file without force) → planning_materialize fails atomically, planning_materialize_status shows atomic=false with the refusal and lastAttempt stamped while lastSuccess keeps the older value; a clean force run then flips lastSuccess and persists the full per-file report.
- Suite green; build 0 warnings.
</verification>