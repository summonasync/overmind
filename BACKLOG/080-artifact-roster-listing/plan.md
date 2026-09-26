---
feature: 080
slug: artifact-roster-listing
status: validated
created: 2026-09-26
validated: 2026-09-26
---

# Plan 080 — Artifact roster: planning_list_artifacts — metadata-only listing per feature (order/kind/status/review/AC/brief)

<approach>
Three small tasks: the agent-door roster endpoint (server-side projection — artifacts queried per feature, task bodies parsed for AC counts via the existing PlanningSections parser, bodies never leave the server), the thin MCP tool mirroring it, and the two doc anchors (CONTEXT surface count, STRUCTURE read ladder).

- No Core/Infrastructure changes: the handler reuses the GetFeature feature-resolution path (slug → 404 shape) and the existing AC-count helper from `PlanningSections` (039). Feature-scoped query, no pagination (spec).
- Door scope: agent door + MCP only. Web keeps reading through read_feature until the UI adopts the listing (feature non-goal: no web UI change).
- Sorting (spec, agent default): documents `spec` → `plan` → `result` first, then tasks by `Order` asc; `kind` filter narrows the same projection. Task-only fields (`review`, `acTicked`/`acTotal`, `brief`) are omitted on non-task items — same convention as read_feature's review field.
- Unknown `kind` → 400 listing the four allowed values (agent-door error mapping). Pins follow house patterns: verbatim description pin + live ListToolsAsync (032), canned-stub handler tests (037 MCP projection pattern; 040 Api GET pattern).
</approach>

<tasks>
- [ ] 01-roster-endpoint — Host: `GET /api/agent/projects/{projectId:guid}/planning/features/{slug}/artifacts?kind=` — route added to the `MapAgentPlanningApi` block (src/Overmind.Host/Api/AgentPlanningApi.cs:35-61); handler `internal static ListArtifacts` in PlanningFeaturesApi (reuse GetFeature's resolution/404). Response `{ artifacts: [...] }`, per artifact: `artifactId, order, kind, status, version, updatedAt` + task-only `review, acTicked, acTotal, brief`. Sort: spec, plan, result, then Order asc. `kind` validated against the four names → 400 with allowed values. Tests (tests/Overmind.Api.Tests/ — AgentPlanningApiTests.cs or a sibling roster file): mixed-feature roster shape+order, kind=task filter, unknown kind 400, unknown feature 404, task-only fields absent on documents. Gates: dotnet build 0W/0E + full suite green → tasks/01-roster-endpoint.md
- [ ] 02-mcp-list-artifacts-tool — Mcp: `planning_list_artifacts(projectId, slug, kind?)` in src/Overmind.Mcp/OvermindPlanningTools.cs — ReadOnly+Idempotent, `Guarded` passthrough to the new GET; description carries the roster rung verbatim: "metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains; planning_read_artifact for one body, planning_read_feature for the whole feature". Tests (tests/Overmind.Mcp.Tests/): registered with right name/schema (live ListToolsAsync), description verbatim pin (032 pattern), canned-stub happy path + kind filter + task-only-field passthrough, API error → tool error. Gates: build + tests green → tasks/02-mcp-list-artifacts-tool.md
- [ ] 03-docs-anchors — Docs: CONTEXT.md:150 MCP surface bullet — "twenty-two" → "twenty-three", roster tool added to the enumeration; STRUCTURE.md:50 read ladder gains the roster rung (cheapest: list before read). Grep-verified; no web/README changes. Gates: grep pins + build unaffected → tasks/03-docs-anchors.md
</tasks>

<risks>
- **AC counts require body access.** No dedicated AC column exists — `acTicked/acTotal` come from parsing task bodies (PlanningSections, 039). The endpoint loads task bodies server-side (tens of KB per feature, never sent to the client); a documents-only `kind` filter skips bodies entirely. Reviewer should reject any caching of parsed counts (stale-tick risk).
- **Brief truncation rule.** Brief is stored capped (max 500 chars, house upsert rule) — the endpoint must NOT re-truncate or pad; pass the stored value through.
- **Door-shape drift.** The roster response must agree with read_feature's metadata field names (`artifactId`, `acTicked`/`acTotal`, `review`) — drift between doors is a review finding (040 precedent).
</risks>

<verification>
- dotnet build 0W/0E + full test suite green (baseline 1344).
- Live: planning_list_artifacts on feature 080 returns 3 tasks metadata-only; kind=task narrows; planning_read_feature unchanged.
- Grep pins: CONTEXT "twenty-three", STRUCTURE roster rung present.
</verification>

<log>
- 2026-09-26 [agent] — plan drafted after code recon (squeezr-assisted): agent-door + MCP only, PlanningSections reused for AC counts, three tasks with verifiable ACs.
- 2026-09-26 [agent] — validated with human proof: notification d7c87711-8f0b-4177-a979-8f7567345a54 replied 2026-09-26 20:54:13 UTC
</log>