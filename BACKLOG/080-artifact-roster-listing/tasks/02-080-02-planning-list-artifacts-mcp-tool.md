---
feature: 080
task: 02
slug: artifact-roster-listing
status: done
review: light
created: 2026-09-26
brief: MCP planning_list_artifacts(projectId, slug, kind?) — ReadOnly passthrough + verbatim description pins
---

# 080/02 — planning_list_artifacts MCP tool

<goal>
`planning_list_artifacts(projectId, slug, kind?)` — ReadOnly+Idempotent MCP tool mirroring the new agent-door roster GET: metadata-only listing of one feature's artifacts, the cheapest way to see what a feature contains before reading any body.
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 01 (roster GET) landed — the tool is a thin passthrough over `/api/agent/projects/{projectId}/planning/features/{slug}/artifacts?kind=`. Adapter pattern: `[McpServerToolType] OvermindPlanningTools` → `Guarded(() => _api.GetAsync(...))`.
</context>

<scope>
- `src/Overmind.Mcp/OvermindPlanningTools.cs` — add the tool next to planning_read_feature:
  `[McpServerTool(Name = "planning_list_artifacts", ReadOnly = true, Idempotent = true)]`
  Description VERBATIM: `Metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains; planning_read_artifact for one body, planning_read_feature for the whole feature.`
- Params: `Guid projectId`, `string slug`, `string? kind`, `CancellationToken` — every parameter carries a [Description].
- Passthrough: return the GET result unchanged. Do NOT route through KeepReviewOnlyOnTaskArtifacts — the roster response already omits review on documents server-side.
- kind forwarding: omit the query param when null/empty; pass through otherwise (server validates and 400s).
- Tests (`tests/Overmind.Mcp.Tests/` — follow the 032 pin pattern + existing canned-stub tool tests):
  (a) live ListToolsAsync — tool registered with the exact name;
  (b) description verbatim pin;
  (c) canned-stub happy path — metadata payload passes through unchanged (no re-shaping, review only on tasks as served);
  (d) kind forwarded in the request URL when set, absent when null;
  (e) API error (404 stub) surfaces as tool error via Guarded, not a crash.
</scope>

<anti-scope>
- No Host changes (task 01 owns the GET), no Core/Infrastructure changes, no web.
- No SessionBootstrapPrompt/read-routing line changes — roster joins the tool surface, not the read ladder text (docs task 03 owns CONTEXT/STRUCTURE wording).
- No response projection of its own (server-side response is already compact).
</anti-scope>

<constraints>
- No comments; English strings; no new packages; NO git commits.
- `dotnet build Overmind.sln` 0W/0E; full `dotnet test` green (baseline 1344 + task 01 additions).
- git status: changes only under src/Overmind.Mcp + tests/Overmind.Mcp.Tests.
</constraints>

<acceptance-criteria>
- [x] planning_list_artifacts registered (exact name, ReadOnly+Idempotent) in live ListToolsAsync.
- [x] Tool + parameter descriptions match the verbatim pins.
- [x] Happy path passes the roster JSON through unchanged; kind forwarded/omitted correctly.
- [x] API errors surface as tool errors (Guarded path pinned).
- [x] dotnet build 0W/0E + full suite green; diff confined to Mcp + Mcp tests.
</acceptance-criteria>

<refs>
- src/Overmind.Mcp/OvermindPlanningTools.cs (Guarded pattern, planning_read_feature at ~:80-98)
- GET /api/agent/projects/{projectId}/planning/features/{slug}/artifacts (task 01)
- tests/Overmind.Mcp.Tests/ (032 description-pin pattern, canned-stub tests)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-26 — Task created from validated plan 39417216; dispatch after task 01 (read-after-write on the new GET).
- 2026-09-26 [agent] — Coder done: planning_list_artifacts (ReadOnly+Idempotent, verbatim description, kind forwarded when set / omitted when null/whitespace, pure passthrough — no KeepReviewOnlyOnTaskArtifacts). Tests: registered in the 23-tool pin + 33-tool handshake pin, description verbatim pin, roster passthrough pinned via DeepEquals against the canned payload (review/brief/acTicked/acTotal only on tasks as served, no body/log reshaping), kind forwarded in URL, 404 stub surfaces as tool error with process survival. New canned PlanningArtifactRosterJson + stub route in RegisterPlanningRoutes. Suite 1529 → 1533 (+4), build 0W/0E. Hand edits confined to src/Overmind.Mcp + tests/Overmind.Mcp.Tests; Host/docs changes in the working tree belong to task 01 / materialization. To review.
- 2026-09-26 [agent] — Coder finished, moved to review: all 5 acceptance-criteria verified for real (build 0W/0E, suite 1533 green incl. 4 new contract tests, diff confined to Mcp + Mcp tests). Awaiting reviewer.
- 2026-09-26 [agent] — light review PASS (0 BLOCKER / 0 IMPORTANT / 2 NIT no-action) — audits/review-080-02.md; независимые гейты: build 0W/0E, 1533/1533 с первого прогона, verbatim-пин побайтный, passthrough чистый (KeepReview не задействован); done by orchestrator after review
</log>