---
feature: 010
task: 02
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/02 — Current flag: HTTP API + MCP tool

<goal>
The current pin is reachable through both doors: HTTP `POST /api/projects/{projectId}/planning/features/{slug}/current` and the MCP tool `planning_set_current`. The board response carries `isCurrent` per feature.
</goal>

<context>
Task 010/01 added PlanningFeature.IsCurrent and PlanningFeatureService.SetCurrentAsync. This task exposes it. Hard rule: the Host↔MCP contract is additive-only — new fields are appended, nothing renamed or removed. The executing subagent reads ONLY this file.
</context>

<scope>
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs — `SetPlanningFeatureCurrentRequest(bool IsCurrent)` record; append `bool IsCurrent` as the LAST positional parameter of PlanningFeatureBoardItem
- src/Overmind.Host/Api/Mapping/PlanningMapper.cs — ToBoardItem passes feature.IsCurrent
- src/Overmind.Host/Api/PlanningFeaturesApi.cs — `group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/current", SetCurrent)`; handler validates, calls service.SetCurrentAsync, maps PlanningException via PlanningErrors.Map, returns the updated board item or feature response (mirror the artifacts/{id}/status endpoint's shape conventions)
- src/Overmind.Mcp — OvermindApiClient gains SetPlanningFeatureCurrent(projectId, slug, isCurrent); OvermindPlanningTools gains `[McpServerTool(Name = "planning_set_current", Destructive = false, Idempotent = true)]` with params projectId, slug, isCurrent — follow the existing SetStatus tool's error handling pattern
- tests/Overmind.Api.Tests — endpoint tests: pin → 200 and isCurrent true on board; pin second feature → first unpinned; unknown slug → 404
- tests/Overmind.Mcp.Tests — tool registered with the right name/schema; happy path through the stubbed API
</scope>

<anti-scope>
- No Core/Infrastructure logic changes (that was task 01); if a gap is found there, report it in <obstacles>, don't fix silently
- No web changes (tasks 03-04)
- planning_list_features needs no change (it passes board JSON through) — verify isCurrent shows up, do not re-shape the payload
</anti-scope>

<constraints>
- Additive-only DTO changes; FluentValidation for the request if the project pattern calls for it (check how SetPlanningArtifactStatusRequest is validated)
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln --no-build` green. Cold-start flake: `Internal CLR error 0x80131506` → `dotnet build-server shutdown`, retry
</constraints>

<acceptance-criteria>
- [ ] POST .../features/{slug}/current {isCurrent:true} pins; board item shows isCurrent
- [ ] Invariant holds through the API (pin B → A unpinned)
- [ ] planning_set_current tool works via the MCP adapter and surfaces API errors as tool errors (not crashes)
- [ ] dotnet build + dotnet test green
</acceptance-criteria>

<refs>
- @src/Overmind.Host/Api/PlanningArtifactsApi.cs (or wherever artifacts/{id}/status lives) — endpoint shape to mirror
- @src/Overmind.Mcp/OvermindPlanningTools.cs — SetStatus/AppendLog patterns
- tasks/01-current-flag-core.md — prerequisite (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Started: HTTP current endpoint + planning_set_current MCP tool
- 2026-09-18 — Done: current endpoint on both doors + planning_set_current tool; 557/557 green; light review APPROVED (3 NIT: Canned fixture title, unpin path untested at API level, no IsCurrent echo in response)
</log>
