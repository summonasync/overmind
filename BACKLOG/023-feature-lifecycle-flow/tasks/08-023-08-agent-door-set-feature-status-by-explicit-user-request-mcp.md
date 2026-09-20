---
feature: 023
task: 08
slug: feature-lifecycle-flow
status: done
review: light
created: 2026-09-20
brief: Agent door: planning_set_feature_status MCP tool — done-by-request with mandatory logNote, fired only on explicit in-session user request; race-hint on InvalidFeatureTransition.
---

# 023/08 — Agent door: set feature status by explicit user request (MCP)

<goal>
Agents get an MCP path to move a feature status (primarily done) when the user explicitly asks in-session — closing the Fact-3 gap (today the only path is the human-door HTTP PUT, no logNote, races with auto-promotions).
</goal>

<context>
User decision 2026-09-20 during 025 closeout: «нам нужен mcp чтобы ставить Done by agent, по запросу». The 023 spec recorded "agent door does NOT get a PUT mirror" as a non-goal — superseded by this task for the done-by-request case (decision note on the board: facts 1-3 dogfood notes). Live pain: orchestrator finishes a feature, human door requires the user to click; with escape-hatch deployed the UI click is trivial but still a context switch for the user.
</context>

<scope>
- MCP tool planning_set_feature_status (agent door): mirrors the human-door PUT validation (transitions matrix, typed 400 on violation) with MANDATORY logNote (board log line records who asked: "by user request in-session").
- Authorization model: the agent may call it ONLY on explicit user request in the current session (prompt-verified); the tool itself does not enforce identity — the logNote carries the provenance. InvalidFeatureTransition response gains a retry hint (current status in payload) for auto-promotion races.
- Open design question to settle at spec-time: done-only (bury/close) vs full matrix mirror; default proposal = done-only + inProgress rollback, matching the two flows agents actually drive.
- Tests: Mcp adapter tool wiring, validation parity with human door, logNote mandatory (400 without).
</scope>

<anti-scope>
- No auto-done by agents without user request; no permission system/identity enforcement; no changes to auto-promotions or the matrix itself.
</anti-scope>

<constraints>
- Human door stays authoritative; the MCP tool is a delegated hand, not a second policy source (same PlanningFeatureService path).
</constraints>

<acceptance-criteria>
- [ ] planning_set_feature_status callable via MCP; done flip works end-to-end with logNote recorded.
- [ ] Refuses (typed 400) without logNote; validates against the transitions matrix.
- [ ] InvalidFeatureTransition payload carries current status.
- [ ] dotnet test green; live dogfood: close a scratch feature via the tool by user request.
</acceptance-criteria>

<refs>
- Board notes: dogfood facts 1-3 (2026-09-20); decision entry escape-hatch; 023 spec done-semantics non-goal (superseded here, tombstone at execution).
- Code: Mcp adapter planning tools, PlanningFeatureService.UpdateAsync, human door PUT route.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Carved on user request during 025 closeout human-door discussion.
- 2026-09-20 — Starting execution per user request ("ну так давай сделаем 023/08"). Design settled per the task default: agent tool allows done (from any non-done status) + inProgress (review rollback) only; mandatory logNote; same PlanningFeatureService validation path as the human door; InvalidFeatureTransition carries current status.
- 2026-09-20 — Done: MCP tool planning_set_feature_status + agent route POST /api/agent/projects/{id}/planning/features/{slug}/status — done (any non-done source) + inProgress (review rollback) only, logNote mandatory at 3 layers, same PlanningFeatureService/Transitions validation as the human door, teaching error for manual-only moves. Review PASS (audits/review-023-08.md; W-1 nullable warnings fixed, 4 INFO → microbatch 24-25). 942/942, build 0 warnings. Live dogfood on deployed 0.1.9: negative ready→400 FeatureStatusNotAllowedForAgent (teaching text), no-logNote→400, positive draft→done 200, logNote persisted as dated line in feature body AND materialized feature.md; scratch cleaned. Spec v3 tombstones the old non-goal.
</log>
