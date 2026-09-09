---
feature: 006
task: 04
slug: agent-planning-api
status: todo
review: light
created: 2026-09-08
---

# 006/04 — Agent door `/api/agent/planning/*`

<goal>
Agents get their own HTTP door: the reads and writes the MCP adapter needs, an upsert-style artifact route, a `current` endpoint that hands the agent either its next task or an explicit STOP state, and a `validate` route that refuses with 409 when human validation is effectively required.
</goal>

<context>
Tasks 02 and 03 are in: the human door (`PlanningFeaturesApi`, `PlanningArtifactsApi`) has `internal static` handlers, and `PlanningPolicy` resolves the effective gate.

This task adds **no new write logic** — the agent door maps onto the same handler delegates so R2 ("HTTP API and MCP share one service path") holds by construction. Only two behaviors are agent-specific: the gated `validate` and `current`.

Amended per plan A1: since task 02 the handlers themselves are thin wrappers over `PlanningFeatureService` / `PlanningArtifactService` (`Infrastructure/Planning`), so the shared path is the **service**, not the handler — the agent upsert route is `PlanningArtifactService.UpsertAsync`, and conflict/not-found translation arrives as `PlanningException` from the service rather than being re-implemented here. Do not add service methods in this task; if the agent door needs behavior the services do not have, stop and record it in `<obstacles>`.

The MCP adapter (task 07) will be the only caller. Route shapes must match the human door's DTOs so the adapter can reuse one set of tolerant `JsonElement` reads.
</context>

<scope>
`src/Overmind.Host/Api/AgentPlanningApi.cs` (`MapAgentPlanningApi`), registered in `Program.cs`:

Reads (same DTOs as the human door):
- `GET /api/agent/projects/{projectId:guid}/planning/features` → board.
- `GET /api/agent/projects/{projectId:guid}/planning/features/{slug}` → full feature.
- `GET /api/agent/projects/{projectId:guid}/planning/features/{slug}/current` → new response below.

Writes (mapped onto the task-02 handlers):
- `POST /api/agent/projects/{projectId:guid}/planning/features` → create feature.
- `POST /api/agent/projects/{projectId:guid}/planning/features/{slug}/artifacts` → **upsert**: when an artifact of the same `Kind` and `Order` exists, its body is updated as a new version (frozen-artifact rules apply, so a missing `logNote` on a frozen spec/plan returns the same 409 `ChangeNoteRequired`); otherwise a new artifact is created. Document kinds ignore `Order` (always 0).
- `POST /api/agent/planning/artifacts/{id:guid}/status`, `.../log` → the shared handlers.
- `POST /api/agent/planning/artifacts/{id:guid}/validate` → gate check first.

`Api/Contracts/PlanningContracts.cs` (extend):
- `PlanningCurrentResponse(string State, PlanningCurrentFeature Feature, PlanningArtifactResponse? Artifact, string? Reason)` with `State` one of `"task" | "stop" | "empty"` and `PlanningCurrentFeature(string Slug, string Title, PlanningFeatureStatus Status)`.

Gate semantics for `validate`:
- `PlanningPolicy.ResolveValidationGateAsync` → true: return 409 with `title: "HumanValidationRequired"` and `detail: "STOP: the <kind> of feature '<slug>' awaits human validation — ask the user to approve it in the Overmind UI."`. Nothing is written.
- false: delegate to the shared validate handler (self-validation works, `FrozenAt` is stamped).

`current` resolution order:
1. gate on **and** (no Spec artifact, or Spec is `Draft`) → `state: "stop"`, `reason: "STOP: spec awaits human validation — it cannot be approved through MCP."`
2. gate on **and** Spec is `Validated` **and** (no Plan artifact, or Plan is `Draft`) → `state: "stop"`, `reason: "STOP: plan awaits human validation — it cannot be approved through MCP."`
3. no Task artifacts → `state: "empty"`, `reason: "No tasks yet — create them from the validated plan."`
4. all tasks `Done` → `state: "empty"`, `reason: "All tasks are done — write the result artifact."`
5. otherwise → `state: "task"` with the first task by `Order` whose status is not `Done` (a `Blocked` task is skipped in favour of a later actionable one; if every remaining task is `Blocked`, return it with `reason: "The next task is blocked."`).
Gate off: skip steps 1–2 entirely (agents self-validate), keep 3–5.

Tests — `tests/Overmind.Api.Tests/AgentPlanningApiTests.cs`:
- upsert: first call creates a spec, second call with a changed body creates version 2 (verified via the human `GET .../versions`);
- upsert on a frozen spec without `logNote` → 409 `ChangeNoteRequired`; with `logNote` → 200 and status back to `draft`;
- task upsert by explicit order creates distinct tasks, same order updates in place;
- gated validate → 409 `HumanValidationRequired` with the STOP text, artifact stays `draft` and `frozenAt` null;
- ungated validate (global `on`, project `off`) → 200 and `validated`;
- `current` for all five states, including "gate off skips the spec/plan STOP" and "all remaining tasks blocked";
- board and full-feature responses are byte-identical in shape to the human door (assert the same JSON property names).
</scope>

<anti-scope>
- No MCP adapter changes (task 07) — this task is HTTP only.
- No new domain methods; if a rule cannot be expressed with what task 01 built, stop and record it in `<obstacles>` instead of inventing domain behavior here.
- No deletes on the agent door (agents do not delete features/artifacts in v1).
- No rate limiting, auth or auditing of the agent door.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green.
- No new packages. English only. No comments unless non-obvious.
- `AgentPlanningApi.cs` stays under ~300 lines — it should be mostly `Map*` calls plus the two agent-specific handlers.
- Shared handlers are reused, not copy-pasted: a duplicated EF query for an existing use case is a review blocker.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [ ] Every agent route answers with the same DTO shape as its human-door twin.
- [ ] Artifact upsert creates on first call and versions on subsequent calls, honoring the frozen rules.
- [ ] Gated `validate` writes nothing and returns 409 `HumanValidationRequired` with an actionable STOP message.
- [ ] `current` returns `task` / `stop` / `empty` per the resolution order, with the gate-off shortcut.
- [ ] Blocked-task handling matches the rule (skip to a later actionable task, else report the blocked one).
</acceptance-criteria>

<refs>
- @../spec.md — "Lifecycle & gates" (two doors, one code path), "MCP surface"
- @../plan.md — Task 04
- @./02-planning-api.md — shared handlers and contracts
- @./03-planning-workflow.md — `PlanningPolicy.ResolveValidationGateAsync`
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
