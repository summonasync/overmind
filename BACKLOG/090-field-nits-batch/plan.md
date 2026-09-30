---
feature: 090
slug: field-nits-batch
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Plan 090 — Field-report nits batch: brief/title patch, feature body-only read, 404 alias hints, no-plan quickstart line (бывш. 082)

## Shape

Four sequential tasks. All tasks touch `src/Overmind.Mcp/OvermindPlanningTools.cs`, so parallel dispatch is off the table — a shared file means sequential. Backend pieces are tiny and ride with their item.

Conventions: xUnit + Shouldly, tests live next to the layer they exercise (`Overmind.Api.Tests/PlanningApiTests.cs` for HTTP behavior, `Overmind.Mcp.Tests` for adapter behavior). No migrations, no UI changes, no new packages.

## Task 1 — brief-patch (R1)

Files:
- `src/Overmind.Host/Api/Contracts/PlanningContracts.cs` — `PatchPlanningArtifactRequest` gains `string? Brief`.
- `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs` — `PatchAsync`: exactly-one-of {section, tick, brief}; brief branch calls `artifact.SetBrief(patch.Brief)`, task-only (`BriefNotAllowedForKind` otherwise), feature.Touch, save + materialization sync like the other branches.
- `src/Overmind.Core/Abstractions/PlanningException.cs` — `InvalidPatchRequest` message names all three mutually exclusive actions.
- `src/Overmind.Mcp/OvermindPlanningTools.cs` — `planning_patch_artifact` gains `brief` param + description (task-only, max 500, no version bump); op-builder extended.
- Tests in `tests/Overmind.Api.Tests/PlanningApiTests.cs`: brief on task → 200, version unchanged, body unchanged; brief on spec → 400 `BriefNotAllowedForKind`; section+brief / tick+brief → 400 `InvalidPatchRequest`.

Verification: `dotnet build` + new tests green.

## Task 2 — includeArtifacts on feature read (R2)

Files:
- `src/Overmind.Host/Api/PlanningFeaturesApi.cs` — `GetFeature` query param `includeArtifacts` (default true).
- `src/Overmind.Host/Api/Contracts/PlanningContracts.cs` — compact roster item shape (kind, order, status, brief for tasks, acTicked/acTotal) when false; full response unchanged when true/default.
- `src/Overmind.Mcp/OvermindPlanningTools.cs` — `planning_read_feature` gains the flag, passes through, description updated ("body + roster summary without artifact bodies — the cheap read").
- Tests in `PlanningApiTests.cs`: default response byte-compatible with today's contract; `includeArtifacts=false` → no artifact bodies/logs, roster summary present.

Verification: `dotnet build` + contract tests green.

## Task 3 — alias addressing + 404 hints + R5 descriptions (R4, R5)

Files:
- `src/Overmind.Mcp/OvermindPlanningTools.cs` — optional `projectId` + `alias` on `planning_read_artifact`, `planning_patch_artifact`, `planning_set_status`, `planning_append_log`; alias parser (`{slug}/T{order}`, `{slug}/S|P|R`); resolution via the existing feature-roster read path; unknown alias → error listing the feature's kind/order pairs; alias+artifactId together and alias-without-projectId → client-side `McpException`.
- `src/Overmind.Core/Abstractions/PlanningException.cs` — `MissingArtifact` message gains the alias-format hint + `planning_list_artifacts` recovery pointer.
- `src/Overmind.Host/Api/PlanningArtifactsApi.cs` — the three bare `HttpErrors.NotFound("Artifact …")` sites (GetArtifact, GetVersions, UpdateArtifact) carry the same hint.
- R5: `planning_upsert_artifact` and `planning_current_task` descriptions gain one sentence: a feature without a validated plan never auto-promotes to review — flip manually (`planning_set_feature_status`).
- Tests: MCP adapter tests for alias parse/resolve/error-diagnostics + client-side rejections; API test asserting the 404 detail contains the alias hint; string assertions on the two tool descriptions carrying the no-plan rule.

Verification: `dotnet build` + `dotnet test` full suite green.

## Task 4 — closeout: live dogfood + architect light + result

- Live dogfood on this very feature through the real MCP surface: brief-patch a task (version must not move), `planning_read_feature(includeArtifacts=false)` on `090`, alias read `090/T1`.
- Architect light review → `audits/architect-review-2026-09-29-090.md`; fix blockers if any.
- Result artifact draft with verification evidence.

Verification: full `dotnet test` green; dogfood transcript recorded in the result.

## Risks

- PatchAsync's one-action XOR is load-bearing for existing ops — task 1 must keep section/tick behavior byte-identical for old callers.
- GetFeature response shape is consumed by the web UI — default path must not change (contract test pins it).
- Alias resolution adds a read per aliased call — acceptable at ceremony scale; noted in result, not optimized.

<log>
- 2026-09-29 [agent] — plan drafted after spec validation; 4 sequential tasks, shared-file constraint documented.
- 2026-09-29 [agent] — validated with human proof: notification 078a0410-8f55-4070-9905-6280cc687ee9 replied 2026-09-29 16:37:25 UTC
</log>