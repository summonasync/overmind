---
feature: 006
task: 03
slug: planning
status: done
review: light
created: 2026-09-08
---

# 006/03 — Workflow settings & global defaults

<goal>
Per-project planning workflow configuration exists and resolves against global defaults: `planningMaterialization` (`inherit|off|on|archive`) and `requireHumanValidation` (`unset|on|off`), readable and writable over HTTP, with the effective values computed in one place that later tasks (agent gate, materializer) consume.
</goal>

<context>
Task 01 added `Project.PlanningMaterialization` (default `Inherit`) and `Project.RequireHumanValidation` (default `Unset`) plus `Project.SetPlanningWorkflow(...)`.

Global settings live in the `AppSetting` key/value table through `Infrastructure/Configuration/AppSettingsService` (`GetOrDefaultAsync`, `SetAsync`), with key constants in `SettingKeys` and defaults in `SettingDefaults`. `Host/Api/SettingsApi.cs` shows the read/write pattern: `GetSettings` composes a response from several keys, `UpdateSettings` validates values and returns the composed response again. `SystemRootsApi.cs` shows the newest slice conventions.

`SettingsResponse` is consumed by the MCP-facing HTTP contract only indirectly (the adapter does not read settings), but the additive-only rule still applies: add fields, never rename.
</context>

<scope>
`src/Overmind.Infrastructure/Planning/PlanningPolicy.cs` — exactly two public methods:
- `ResolveValidationGateAsync(Guid projectId, CancellationToken ct)` → `bool` (true = human validation required). Project `On`/`Off` wins; `Unset` falls back to the global `planning.requireHumanValidationDefault`.
- `ResolveMaterializationAsync(Guid projectId, CancellationToken ct)` → `PlanningMaterialization` (never `Inherit`). Project value wins; `Inherit` falls back to the global `planning.materializationDefault`.
Missing project → throw `InvalidOperationException` (handlers 404 before calling, so this is a guard, not a path).

`SettingKeys` + `SettingDefaults`: add `PlanningRequireHumanValidationDefault = "planning.requireHumanValidationDefault"` (default `"off"`) and `PlanningMaterializationDefault = "planning.materializationDefault"` (default `"off"`), plus allowed-value arrays (`["on","off"]` and `["off","on","archive"]`) mirroring the existing `EmbeddingProviders` style. Values are stored lowercase.

`src/Overmind.Host/Api/Contracts/PlanningContracts.cs` (extend):
- `PlanningWorkflowResponse(PlanningMaterialization Materialization, HumanValidationOverride RequireHumanValidation, PlanningMaterialization EffectiveMaterialization, bool EffectiveRequireHumanValidation)`
- `UpdatePlanningWorkflowRequest(PlanningMaterialization? Materialization, HumanValidationOverride? RequireHumanValidation)`

`src/Overmind.Host/Api/PlanningWorkflowApi.cs` (`MapPlanningWorkflowApi`):
- `GET /api/projects/{projectId:guid}/planning/workflow` → stored values + effective values; 404 for an unknown project.
- `PUT /api/projects/{projectId:guid}/planning/workflow` → applies the supplied fields only (null = untouched), persists through `Project.SetPlanningWorkflow`, returns the same shape as GET.
- Unknown enum strings in the body deserialize to nothing usable — reject with a 400 ValidationProblem naming the allowed values instead of silently defaulting.

`SettingsApi` (extend): `SettingsResponse` gains `PlanningMaterializationDefault` and `PlanningRequireHumanValidationDefault`; `UpdateSettingsRequest` gains both as optional strings; `UpdateSettings` validates against the allowed-value arrays (same `InvalidSetting` helper style) and stores lowercase.

DI: register `PlanningPolicy` (scoped) in `DependencyInjection.AddOvermind`, and the workflow validator if one is added.

Tests:
- `tests/Overmind.Api.Tests/PlanningWorkflowApiTests.cs` — GET defaults (`inherit`/`unset` + effective `off`/`false`); PUT materialization `on` → effective `on`; PUT validation `on` → effective true; project `off` beats a global `on`; project `unset` follows a global flip; unknown project → 404; bad enum string → 400.
- `tests/Overmind.Api.Tests/SettingsApiTests.cs` (extend) — new defaults present with their out-of-the-box values, writable, persisted across a second GET, invalid values rejected.
- `tests/Overmind.Infrastructure.Tests/PlanningPolicyTests.cs` — resolution matrix against a temp DB (project override vs global default for both settings).
</scope>

<anti-scope>
- No materialization implementation (task 05) — this task only resolves which strategy applies.
- No agent-door gate enforcement (task 04) — this task only exposes `ResolveValidationGateAsync`.
- No web UI for these settings (tasks 08/09).
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green.
- No new packages. English only. No comments unless non-obvious.
- `PlanningPolicy` stays at two public methods (house ceiling is three).
- Additive-only changes to `SettingsResponse` / `UpdateSettingsRequest`.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [x] Effective values follow the matrix: project override wins, `unset`/`inherit` fall back to the global default.
- [x] Both global defaults are `off` out of the box and are writable through `PUT /api/settings`.
- [x] `GET`/`PUT /api/projects/{id}/planning/workflow` return stored **and** effective values.
- [x] Invalid enum strings produce a 400 ValidationProblem naming allowed values, never a silent default.
</acceptance-criteria>

<refs>
- @../spec.md — "Lifecycle & gates" (two-level gate), "Materialization" (strategy + global default)
- @../plan.md — Task 03
- src/Overmind.Infrastructure/Configuration/{SettingKeys,SettingDefaults,AppSettingsService}.cs
- src/Overmind.Host/Api/SettingsApi.cs, src/Overmind.Host/Api/SystemRootsApi.cs
- @./01-domain-persistence.md — Project workflow properties
</refs>

<obstacles>
- `UpdatePlanningWorkflowRequest` uses `string?` fields instead of the enum-typed fields listed in the scope. Rationale: with `JsonStringEnumConverter` an unknown enum string throws during model binding and the framework answers with a generic 400 before the handler runs — the required "400 ValidationProblem naming the allowed values" is impossible to produce. String fields let the handler `Enum.TryParse` (case-insensitive, `Enum.IsDefined` guard against numeric strings) and return the ValidationProblem itself. The response contract `PlanningWorkflowResponse` keeps enum types as specified (serialized camelCase: `inherit`/`unset`/`off`/`on`/`archive`).
- `PlanningPolicy` resolves the effective values through a second `FindAsync` on the same scoped `OvermindDbContext` in the PUT handler — no extra round-trip, the tracked entity is returned.
- Cold `dotnet build` hit the known "Internal CLR error 0x80131506"; `dotnet build-server shutdown` + retry fixed it (environment, not code).
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
2026-09-12 — executed. Changes by file:

**src/Overmind.Infrastructure/Configuration/SettingKeys.cs** — added `PlanningMaterializationDefault` (`planning.materializationDefault`) and `PlanningRequireHumanValidationDefault` (`planning.requireHumanValidationDefault`).

**src/Overmind.Infrastructure/Configuration/SettingDefaults.cs** — added both defaults (`"off"`) plus allowed-value arrays `PlanningMaterializationDefaults` (`["off","on","archive"]`) and `PlanningRequireHumanValidationDefaults` (`["on","off"]`), mirroring the `EmbeddingProviders` style.

**src/Overmind.Infrastructure/Planning/PlanningPolicy.cs** — new, exactly two public methods: `ResolveValidationGateAsync` (project `On`/`Off` wins, `Unset` falls back to the global default) and `ResolveMaterializationAsync` (project value wins, `Inherit` falls back to the global default, never returns `Inherit` — an unparseable/`inherit` global value degrades to `Off`). Missing project → `InvalidOperationException` guard.

**src/Overmind.Host/Api/Contracts/PlanningContracts.cs** — added `PlanningWorkflowResponse` (stored + effective values, enum-typed) and `UpdatePlanningWorkflowRequest` (string-typed fields — see `<obstacles>`).

**src/Overmind.Host/Api/Contracts/SettingsContracts.cs** — additive: `SettingsResponse` and `UpdateSettingsRequest` gained `PlanningMaterializationDefault` / `PlanningRequireHumanValidationDefault`.

**src/Overmind.Host/Api/PlanningWorkflowApi.cs** — new slice `MapPlanningWorkflowApi`: `GET`/`PUT /api/projects/{projectId:guid}/planning/workflow`, 404 for unknown project, PUT applies supplied fields only (null = untouched) through `Project.SetPlanningWorkflow`, unknown enum strings → 400 ValidationProblem naming the allowed values.

**src/Overmind.Host/Api/SettingsApi.cs** — `GetSettings` composes the two new defaults; `UpdateSettings` validates them against the allowed-value arrays (case-insensitive) via the existing `InvalidSetting` helper and stores lowercase.

**src/Overmind.Host/DependencyInjection.cs** — registered `PlanningPolicy` (scoped). No workflow validator was added (manual parse in the handler), so no extra registration.

**src/Overmind.Host/Program.cs** — added `MapPlanningWorkflowApi()`.

**tests/Overmind.Api.Tests/PlanningWorkflowApiTests.cs** — new, 8 tests: GET defaults (`inherit`/`unset` + effective `off`/`false`), PUT materialization `on` → effective `on`, PUT validation `on` → effective true, project `off` beats a global `on`, project `unset` follows a global flip, GET/PUT unknown project → 404, bad enum strings → 400 ValidationProblem naming allowed values and leaving stored values untouched.

**tests/Overmind.Api.Tests/SettingsApiTests.cs** — extended with 3 tests: new defaults present as `off` out of the box, writable (case-insensitive input stored lowercase) and persisted across a second GET, invalid values rejected with 400.

**tests/Overmind.Infrastructure.Tests/PlanningPolicyTests.cs** — new, 7 tests on a temp DB: the full resolution matrix (project override vs global default for both settings), global materialization never resolving to `inherit`, missing project → `InvalidOperationException`.

**Route table:**
| Method | Path | Success | Error |
|--------|------|---------|-------|
| GET | /api/projects/{projectId}/planning/workflow | 200 stored + effective | 404 unknown project |
| PUT | /api/projects/{projectId}/planning/workflow | 200 stored + effective | 404 unknown project, 400 bad enum string |

**Verification:** `dotnet build Overmind.sln` → 0 warnings / 0 errors. `dotnet test Overmind.sln` → 316 passed, 0 failed (Core 85, Infrastructure 85, Mcp 34, Api 112); +18 over the 298 baseline.

**Notes for task 04 (agent door):** call `PlanningPolicy.ResolveValidationGateAsync` before ceremony mutations; it is the single resolution point.
**Notes for task 05 (materializer):** call `PlanningPolicy.ResolveMaterializationAsync` to pick the strategy; it never returns `Inherit`.
</log>
