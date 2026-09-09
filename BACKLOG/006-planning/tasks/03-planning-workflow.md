---
feature: 006
task: 03
slug: planning-workflow
status: todo
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
- [ ] Effective values follow the matrix: project override wins, `unset`/`inherit` fall back to the global default.
- [ ] Both global defaults are `off` out of the box and are writable through `PUT /api/settings`.
- [ ] `GET`/`PUT /api/projects/{id}/planning/workflow` return stored **and** effective values.
- [ ] Invalid enum strings produce a 400 ValidationProblem naming allowed values, never a silent default.
</acceptance-criteria>

<refs>
- @../spec.md — "Lifecycle & gates" (two-level gate), "Materialization" (strategy + global default)
- @../plan.md — Task 03
- src/Overmind.Infrastructure/Configuration/{SettingKeys,SettingDefaults,AppSettingsService}.cs
- src/Overmind.Host/Api/SettingsApi.cs, src/Overmind.Host/Api/SystemRootsApi.cs
- @./01-domain-persistence.md — Project workflow properties
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
