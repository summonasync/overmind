---
feature: 090
slug: field-nits-batch
status: validated
created: 2026-09-30
validated: 2026-09-30
---

# Result 090 — Field-report nits batch: brief/title patch, feature body-only read, 404 alias hints, no-plan quickstart line (бывш. 082)

## Outcome

All five spec items landed. R1 brief-patch (patch action, task-only, no version bump), R2 `includeArtifacts` on the feature read (compact roster, byte-pinned default), R3 superseded tombstone (recorded in the spec — no code, 070 owns it), R4 slug-alias addressing on four MCP tools + 404 hints across all five not-found doors, R5 no-plan rule in the two affected tool descriptions. Spec and plan validated before any task; tasks ran strictly sequentially (shared `OvermindPlanningTools.cs`).

## Verification

- Full suite, project-scoped sequential runs: **1688/1688** (Core 225, Infrastructure 535, Api 519, Mcp 409); build 0 warnings / 0 errors.
- Three light reviews PASS-WITH-NITS, 0 blockers (`audits/review-090-01.md`, `-02.md`, `-03.md`); architect light **APPROVED-WITH-WARNINGS** (`audits/architect-review-2026-09-30-090.md`) — 1 IMPORTANT (echo of microbatch #150, brief-cap at the service seam), 3 NIT, 1 SUGGESTION, 3 OBSERVATION, all queued.
- **Live dogfood 5/5** through the real MCP stdio against an isolated fresh-build host (scratch HOME copy of the prod board, port 4799, host killed by PID, prod untouched; adapter pointed via `OVERMIND_API_URL`):
  1. `tools/list` shows the new wire surface: `alias`+`projectId` on read_artifact/set_status/append_log/patch, `brief` on patch, `includeArtifacts` on read_feature.
  2. `planning_read_feature(includeArtifacts=false)` → roster only: spec/plan validated, four tasks with status+brief, zero artifact bodies.
  3. Alias read `090-field-nits-batch/T1` → resolved to exactly task 1's GUID (5fadb47b…, version 5, status done).
  4. Alias brief-patch on `…/T4` → echo `version: 1` (unchanged), `section: "brief"`, body intact.
  5. Unknown GUID → honest 404 carrying the full alias-format hint + `planning_list_artifacts` recovery pointer.

## Deviations (all confirmed legitimate by review)

- `AgentPlanningApi.cs` entered task 1's scope post-hoc: without the `set_brief` branch of `CarriesPatchShape` the op dies pre-service and the response mapping NREs — load-bearing, reviewer-confirmed.
- Two consequential files on task 2 (`PlanningContracts.cs`, `PlanningMapper.cs`) — additive response record + mapper, no existing type fits.
- Task 3 closed two bare 404 sites beyond the listed three (agent-door GetArtifact + Validate) — the goal says "every artifact-not-found text teaches the format"; git history confirms all five were bare.
- No-plan assertions live in `PlanningToolContractTests` (Mcp.Tests), not Api.Tests — tool descriptions are attributes of the Mcp assembly, invisible from the Api test project.
- Task 2 survived a host restart mid-run (coder resumed from transcript, no rework needed).

## Follow-ups

- Microbatch queue grew to #165: brief-cap service-seam guard (#150/#163-c), roster sort unification (#155), alias resolver extraction into `ArtifactAliasResolver.cs` (#165-a), 3-op label ternary watch (#165-b), Core-hint layering (#163), PlanningArtifactService method ceiling (#164).
- After the next deploy: restart session MCP adapters, then field-probe aliases against the live host (this session's adapter predates the feature — expected per the deploy legend).
- Docs candidates, not in scope: cheap-read tier line in CONTEXT.md, alias-grammar ADR (architect suggestion).

<log>
- 2026-09-30 [agent] — result drafted: 4/4 tasks done, 3×PASS-WITH-NITS + architect APPROVED-WITH-WARNINGS, 1688/1688, live dogfood 5/5 on isolated fresh build.
</log>