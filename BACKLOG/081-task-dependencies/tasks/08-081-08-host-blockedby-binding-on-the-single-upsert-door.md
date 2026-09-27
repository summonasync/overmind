---
feature: 081
task: 08
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: Host: bind blockedBy on single-upsert door (gap from task 04) + re-tick task 04 AC4
---

# 081/08 — Host: blockedBy binding on the single-upsert door

<goal>
Close the gap found by task 04: the single-upsert door (CreatePlanningArtifactRequest → PlanningArtifactsApi.Upsert path) does not bind blockedBy — only the batch door does. The MCP single tool forwards the field into a payload the host silently drops. Also re-tick AC4 of task 04 (full suite green) once this lands.
</goal>

<context>
Executor reads ONLY this file. Prerequisites: tasks 01-03 done (TaskDependencyService.UnclosedBlockers/StageBlockedBy + batch integration live in PlanningArtifactService; single UpsertAsync already accepts the blockedBy SERVICE parameter — check PlanningArtifactService.UpsertAsync signature; only the Host CONTRACT binding is missing).
</context>

<scope>
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs: CreatePlanningArtifactRequest gains BlockedBy (List<int>?, default null).
- The single-upsert handler path (PlanningArtifactsApi.Upsert / wherever the request maps into the service call) forwards BlockedBy into UpsertAsync — same resolve/stage semantics as the batch (absent = preserve/none, [] = clear).
- Validator: blockedBy items must be non-negative integers (no other new validation — the service owns order resolution).
- Tests (Api.Tests, follow TaskDependencyApiTests patterns): single-upsert with blockedBy creates edges; [] clears; absent preserves; unknown order → 400 naming the order; batch+single parity spot-check.
- After landing: run the FULL suite; if green, tick the AC4 item on task 04 (ae339783-95e7-4c2b-939a-13e1caa57618) via planning_patch_artifact(acText) — the executor left it unticked waiting for the suite to stabilize.
</scope>

<anti-scope>
- No Mcp changes (task 04 already forwards; this task makes the host actually bind). No web, no materialization.
</anti-scope>

<constraints>
- No comments; English; NO git commits.
- dotnet build 0W/0E; full suite green (baseline 1614).
- Diff: Host contracts/handler + Api tests.
</constraints>

<acceptance-criteria>
- [x] Single-upsert POST binds blockedBy: edges created; [] clears; absent preserves.
- [x] Unknown order → 400 naming it; batch+single parity pinned.
- [x] dotnet build 0W/0E + full suite green; diff confined to Host + Api tests.
- [x] Task 04 AC4 re-ticked after the green full-suite run.
</acceptance-criteria>

<refs>
- CreatePlanningArtifactRequest + Upsert handler (Host)
- PlanningArtifactService.UpsertAsync (service parameter exists)
- TaskDependencyApiTests.cs (batch patterns to mirror)
- Task 04 obstacles (gap documentation)
</refs>

<obstacles>
- Single door answers 201 Created (not 200 OK) on a re-upsert that only changes blockedBy or is a full no-op: isNew keys off Version==1 and UpdateBody never runs when the body is unchanged. Harmless for contract semantics (idempotent upsert), but callers must not read the status code as create-vs-update. Pinned with ShouldBeOneOf in the clear/absent tests.
- Scope widened mid-task by the orchestrator (rev-081-04 follow-ups, received while task 08 was in progress): Mcp BuildTaskItem now rejects a non-array blockedBy item value with McpException instead of silently dropping it (was preserve-by-accident), and the canned read_feature/roster fixtures were aligned to live semantics (blocker done → blocked:false, edges kept). This puts Mcp tool + Mcp tests into the diff beyond the original Host + Api tests confinement; the base scope itself stayed inside it.

</obstacles>

<log>
- 2026-09-28 — created by orchestrator from task 04 obstacle (host single-door binding gap); micro-sized.
- 2026-09-27 [agent] — [agent] task 08 implemented: CreatePlanningArtifactRequest gains BlockedBy, CreateArtifactForDoor forwards it into UpsertAsync (UI/agent/MCP single doors share the path), validator pins non-negative orders. 6 Api.Tests added (edges, [] clears, absent preserves, unknown order 400, negative order 400, batch+single parity). Orchestrator add-ons closed: Mcp BuildTaskItem rejects non-array blockedBy with McpException + pin test; canned fixtures aligned to live (blocker done → blocked:false, blockedBy kept). Gates: dotnet build 0W/0E, full suite 1621/1621 green (Core 225, Infrastructure 535, Api 476, Mcp 385). 4/4 ACs ticked; task 04 AC4 re-ticked.
- 2026-09-27 [agent] — [agent] task done, moved to review: host single-door blockedBy binding live, MCP forwarding revived end-to-end; 4/4 ACs ticked, build 0W/0E, full suite 1621/1621; scope widened by orchestrator add-ons (Mcp strict-guard + canned realism) recorded in obstacles.
- 2026-09-27 [agent] — light review PASS (1 WARNING microbatch: single-door эхо без blocked/blockedBy) — audits/review-081-08.md; независимые гейты: build 0W/0E, 1621/1621; оба довеска rev-04 закрыты (McpException-гард, фикстуры live-семантика); done by orchestrator after review
</log>