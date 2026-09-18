---
feature: 010
slug: planning-board-filters
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Plan 010 — Planning board: filters (tags/status/state) + current feature highlight

<approach>
Bottom-up: Core flag + invariant first, then the two doors (HTTP API + MCP) over the same service, then web (pin/accent, then the filter bar), then a live dogfood closeout. The invariant lives in PlanningFeatureService so API and MCP cannot diverge. Board filtering is pure client-side per spec — no query changes. Web filter params ride the existing useSearchParams on the planning page; new param names must not collide with the reserved project/feature.
</approach>

<tasks>
- [ ] 01-current-flag-core — PlanningFeature.IsCurrent + SetCurrent(bool); EF config (default false) + migration AddPlanningFeatureIsCurrent; PlanningFeatureService.SetCurrentAsync clearing other currents of the project in one transaction; Infrastructure tests (pin, re-pin moves, unpin, cross-project isolation) → tasks/01-current-flag-core.md. Verify: dotnet test Overmind.sln
- [ ] 02-current-api-mcp — POST /api/projects/{projectId}/planning/features/{slug}/current with {isCurrent} body (mirrors artifacts/{id}/status shape); PlanningFeatureBoardItem + IsCurrent (additive) + mapper; OvermindApiClient.SetPlanningFeatureCurrent + MCP tool planning_set_current{projectId, slug, isCurrent}; Api.Tests + Mcp.Tests → tasks/02-current-api-mcp.md. Verify: dotnet test Overmind.sln
- [ ] 03-web-current-pin — types isCurrent; api setPlanningFeatureCurrent; mutation with board invalidation; pin toggle button on card (lucide Pin); current card accent (badge + border) + current-first sort in FeatureBoard → tasks/03-web-current-pin.md. Verify: npm run lint && npx tsc --noEmit + npm run build
- [ ] 04-board-filters — filter bar above the grid: feature-status multi-select, spec/plan chip filters (any/validated/not-validated/none per artifact), current-only toggle; state in URL search params (names: status, spec, plan, current — project/feature reserved); AND composition; zero-match empty state with clear-filters → tasks/04-board-filters.md. Verify: npm run lint && npx tsc --noEmit + npm run build
- [ ] 05-live-dogfood-closeout — full build+test, boot host, pin via MCP planning_set_current, verify accent/filter in UI, unpin, materialize BACKLOG, result artifact, close feature → tasks/05-live-dogfood-closeout.md. Verify: live scenario + dotnet test Overmind.sln
</tasks>

<risks>
- Migration on the live dogfood DB: additive bool default false, auto-applied at boot — safe; snapshot profile before first boot anyway (ritual already exists).
- MCP additive-only contract: board item gains a field (allowed); no renames. planning_list_features passes board JSON through, so isCurrent surfaces without adapter changes beyond the new tool.
- URL param collision with the workspace view (project/feature select the view) — filter params must be ignored when feature is set, and cleared state must not leave junk params.
- Enum PlanningArtifactStatus in filter UI: treat draft/todo/inProgress/review/blocked as one "not validated" bucket + explicit "none" (no spec/plan artifact) — do not expose six artifact statuses as filter options.
</risks>

<verification>
- dotnet build Overmind.sln && dotnet test Overmind.sln — green incl. new invariant/API/MCP tests
- npm run lint && npx tsc --noEmit && npm run build in web/ — green
- Live: pin 009 via MCP → card accents and sorts first in UI; filters compose; reload keeps filter state; unpin via UI
- BACKLOG/010-planning-board-filters materializes clean (no stray duplicates)
</verification>