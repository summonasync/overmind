---
feature: 080
task: 01
slug: artifact-roster-listing
status: done
review: light
created: 2026-09-26
brief: Agent-door GET .../features/{slug}/artifacts?kind= — server-side metadata-only roster, docs→tasks sort, 404/400 per convention
---

# 080/01 — agent-door artifact roster endpoint

<goal>
`GET /api/agent/projects/{projectId:guid}/planning/features/{slug}/artifacts?kind=` returns a metadata-only listing of the feature's artifacts — no bodies in the response. This closes the read cliff between the board aggregate and the full planning_read_feature payload.
</goal>

<context>
Executor reads ONLY this file. Spec: feature 080 spec artifact (365d4dff, validated); plan: 39417216 (validated). Feature body: artifactId, order, kind, status, review, acTicked/acTotal, brief (task-only, capped), version, updatedAt. Sort: documents spec → plan → result first, then tasks by Order asc. No pagination.
</context>

<scope>
- `src/Overmind.Host/Api/AgentPlanningApi.cs` — add route `group.MapGet("/projects/{projectId:guid}/planning/features/{slug}/artifacts", ...)` inside `MapAgentPlanningApi` (route block ~:35-61).
- Handler `internal static ListArtifacts` in `src/Overmind.Host/Api/PlanningFeaturesApi.cs` (same file as `GetFeature`) — reuse GetFeature's feature resolution and 404 shape verbatim; optional `string? kind` query param.
- Response shape: `{"artifacts":[...]}` — per artifact: `artifactId, order, kind, status, version, updatedAt`; task artifacts additionally carry `review, acTicked, acTotal, brief`. Documents never carry those four fields (read_feature's review-only-on-tasks convention).
- AC counts: reuse the SAME acTicked/acTotal mapping `planning_read_feature` uses today (find it by grepping `acTicked` under src/Overmind.Host) — do not fork a second parser.
- `kind` filter: one of spec|plan|task|result (case-insensitive); anything else → 400 with the allowed values listed, following the file's existing error mapping (HttpErrors).
- Sorting in-query or in-memory after loading the feature's artifacts; bodies may be loaded server-side for AC parsing but MUST NOT appear in the response.
- Tests: `tests/Overmind.Api.Tests/` — new `AgentArtifactRosterApiTests.cs` (follow AgentPlanningApiTests.cs patterns: ApiFactory, seeded db): (a) mixed feature → shape, docs-first-then-tasks order, task-only fields present on tasks and absent on the spec; (b) `?kind=task` narrows; (c) unknown kind → 400 naming the four values; (d) unknown slug → 404 same shape as GetFeature.
</scope>

<anti-scope>
- No human-door (`/api/projects/...`) route, no web UI.
- No Core/Infrastructure/Mcp changes — Host Api + Api tests only.
- No pagination, no body content, no batching (044), no diffing (045).
</anti-scope>

<constraints>
- No comments; English strings; no new packages; NO git commits.
- `dotnet build Overmind.sln` 0W/0E; full `dotnet test` green (baseline 1344).
- git status at the end shows changes only under src/Overmind.Host + tests/Overmind.Api.Tests.
</constraints>

<acceptance-criteria>
- [x] GET roster returns metadata-only artifacts of the feature — no body fields anywhere in the JSON.
- [x] Documents (spec/plan/result) come first in spec→plan→result order, tasks after by order asc.
- [x] review/acTicked/acTotal/brief appear on task items only.
- [x] kind=task narrows; unknown kind 400 lists spec/plan/task/result; unknown slug 404 matches GetFeature shape.
- [x] dotnet build 0W/0E + full test suite green; diff confined to Host Api + Api tests.
</acceptance-criteria>

<refs>
- src/Overmind.Host/Api/AgentPlanningApi.cs (route block, handler patterns)
- src/Overmind.Host/Api/PlanningFeaturesApi.cs (GetFeature — resolution + 404)
- src/Overmind.Host/Api/PlanningArtifactsApi.cs (040 metadata mapping precedent)
- acTicked mapping used by read_feature (grep in src/Overmind.Host)
- tests/Overmind.Api.Tests/AgentPlanningApiTests.cs (test patterns)
</refs>

<obstacles>
<obstacles>
- Scope pointed at an existing acTicked/acTotal mapping used by planning_read_feature — no such mapping exists in the codebase. Used the single shared AC parser PlanningSections.ParseAcceptanceCriteria (the same one PlanningArtifactService.UntickedAcWarning counts with): acTicked = items with Checked == true, acTotal = all "- " items. No second parser introduced; the counts mapping debuts in this task.
- Test seed initially set a brief on the spec and got 400 BriefNotAllowedForKind — brief is already task-only at write time, which is also why docs can never carry brief in the roster (enforced upstream, not just omitted in the response shape).
- During the session the board's own materialization touched BACKLOG.md and BACKLOG/080-artifact-roster-listing/* on disk (mirror churn from task creation, not hand edits) — outside the diff-confinement constraint but owned by the board, left untouched.
</obstacles>
</obstacles>

<log>
- 2026-09-26 — Task created from validated plan 39417216; dispatch pending.
- 2026-09-26 [agent] — Implemented agent-door GET /projects/{projectId}/planning/features/{slug}/artifacts?kind= — metadata-only roster in PlanningFeaturesApi.ListArtifacts (GetFeature resolution + 404 shape verbatim), PlanningMapper.ToRosterItem (docs bare; tasks carry review/acTicked/acTotal/brief from the shared PlanningSections.ParseAcceptanceCriteria parser), roster contracts, route in MapAgentPlanningApi, 4 new tests in AgentArtifactRosterApiTests (mixed-feature shape/order, kind filter incl. case-insensitive, 400 listing allowed kinds, 404 parity with GetFeature). dotnet build 0W/0E; full suite 1529/1529 green.
- 2026-09-26 [agent] — Task complete, ready for review: roster endpoint implemented per scope, 5/5 AC verified, build 0W/0E, full suite 1529/1529 green, diff confined to src/Overmind.Host + tests/Overmind.Api.Tests.
- 2026-09-26 [agent] — light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 4 NIT + 1 PROCESS, все no-action) — audits/review-080-01.md; независимые гейты ревьюера: build 0W/0E, 1533/1533; шейп совпал с закоммиченным 070 MCP canned-контрактом; task done by orchestrator after review
</log>