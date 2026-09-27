---
feature: 081
task: 04
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: MCP: blockedBy forwarded on upserts; blocked/blockedBy passthrough; no new tools
---

# 081/04 — MCP passthrough: blockedBy in, blocked fields out

<goal>
The MCP surface carries task dependencies without new tools (user decision): upsert forwards blockedBy; current_task/roster/read_feature pass the new fields through.
</goal>

<context>
Executor reads ONLY this file. Prerequisites: tasks 02-03 done. Pattern: existing passthrough tools in src/Overmind.Mcp/OvermindPlanningTools.cs — upsert_artifact/upsert_tasks build request payloads (JsonObject) — add the optional array; current_task/list_artifacts/read_feature are passthroughs (list_artifact does NOT re-shape). Tool count unchanged (24/34 pins stay).
</context>

<scope>
- planning_upsert_artifact + planning_upsert_tasks item objects: optional blockedBy (array of integer orders) forwarded when non-empty; absent/null omitted entirely (server keeps semantics: absent=preserve on update, none on create).
- current_task/list_artifacts/read_feature: passthrough — new blocked/blockedBy fields flow to agents unchanged; canned stubs extended (Canned.cs + McpSessionTestBase routes) to carry the new task-only fields.
- Tests (Mcp.Tests): blockedBy forwarded in payload (present when set, absent when null), stop-state passthrough, roster passthrough, count pins 24/34 unchanged.
</scope>

<anti-scope>
- No new tools, no Host/Core/Infrastructure changes, no web, no bootstrap text (task 07).
</anti-scope>

<constraints>
- No comments; English; NO git commits.
- dotnet build 0W/0E; full suite green (baseline + tasks 01-03).
- Diff: src/Overmind.Mcp + tests/Overmind.Mcp.Tests.
</constraints>

<acceptance-criteria>
- [x] blockedBy forwarded by upsert tools when set; omitted when null (payload pinned).
- [x] current_task stop + roster passthrough carry blocked/blockedBy to agents.
- [x] Tool count pins unchanged (24 planning / 34 total).
- [x] dotnet build 0W/0E + full suite green; diff confined to Mcp + Mcp tests.
</acceptance-criteria>

<refs>
- OvermindPlanningTools.cs upsert_artifact/upsert_tasks payload builders
- Canned.cs + McpSessionTestBase (stub extension pattern)
</refs>

<obstacles>
- Host single-upsert door does not bind blockedBy. The service (PlanningArtifactService.UpsertAsync) accepts blockedBy and the batch item record (PlanningTaskUpsertRequest) carries it, but CreatePlanningArtifactRequest has no BlockedBy field, so the POST /artifacts door drops the forwarded field. Anti-scope forbids Host changes, so planning_upsert_artifact forwards blockedBy at the payload level (pinned by tests); the batch path is live end-to-end. Follow-up for a Host task: add BlockedBy to CreatePlanningArtifactRequest and pass it through CreateArtifactForDoor.
- Empty blockedBy array is forwarded, not omitted: spec line "empty array clears" overrides the task wording "forwarded when non-empty"; omitted/null preserves edges per server semantics. Pinned by tests on both upsert tools.
- Full-suite gate residual: dotnet build 0W/0E and 1609/1614 tests green (Core 225, Infrastructure 535, Mcp 384, Api 465/470). The 5 stable failures are Overmind.Api.Tests BacklogImport* log-level asserts — the importer zone is being edited by a concurrent session (test ctor helpers for the new PlanningFeatureService ctor changed mid-run); Overmind.Api.Tests does not reference Overmind.Mcp, so the residual is unrelated to this diff. AC4 left unticked for that residual; the Mcp-project gates are fully green.

</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch after task 03 (needs host fields live).
- 2026-09-27 [agent] — AC tick: blockedBy forwarding pinned on both upsert tools (present [1,2], empty [], absent)
- 2026-09-27 [agent] — AC tick: task-blocked STOP reason passthrough, current_task artifact blocked/blockedBy, roster and read_feature passthrough pinned
- 2026-09-27 [agent] — AC tick: no tools added or removed; both count pins green in the suite
- 2026-09-27 [agent] — [agent] task 04 implemented: blockedBy param on planning_upsert_artifact, item-level blockedBy in planning_upsert_tasks, description/pins updated; canned stubs carry blocked/blockedBy (read_feature, roster, current_task, new task-blocked STOP fixture); 3 of 4 ACs ticked, obstacles record the host single-door gap and the unrelated Api.Tests residual
- 2026-09-27 [agent] — [agent] task done, moved to review: MCP layer complete per scope (forwarding + passthrough, no new tools); 3/4 ACs ticked, AC4 held by an unrelated concurrent Api.Tests residual documented in obstacles
- 2026-09-27 [agent] — light review PASS (0 BLOCKER / 0 IMPORTANT; WARNING-1 non-array blockedBy batch-drop → microbatch/08; NIT-2 fixture realism → 08) — audits/review-081-04.md; независимые гейты: build 0W/0E, Mcp 384/384, полный сьют 1614/1614 оркестратором после лендинга 05 (AC4 закрыт фактом); host-гэп single-двери заведён хвост-таской 08; done by orchestrator after review
</log>