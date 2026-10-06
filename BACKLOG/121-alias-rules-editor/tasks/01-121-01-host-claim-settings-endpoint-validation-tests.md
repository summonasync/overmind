---
feature: 121
task: 01
slug: alias-rules-editor
status: done
review: light
created: 2026-10-06
brief: Host: GET/PUT /api/projects/{id}/claim-settings (PlanningWorkflowApi pattern) + validator + MaxNamingRulesLength guard in SetClaimSettings + API/entity tests incl. onboard round-trip leg
---

# 121/01 — Host: claim-settings endpoint + validation + tests

<goal>
The naming-rules text (and the coupled claim-stale threshold) becomes writable through the API: GET/PUT /api/projects/{projectId}/claim-settings, validated, with an entity-level guard mirroring SetManifest. This is the only writer the feature adds; the web section and the adapter rendering consume its contract.
</goal>

<context>
099 shipped Project.AgentNamingRules + SetClaimSettings but no endpoint calls them — the existing API test seeds via raw SQL. Spec dc030eba-1f10-4bf2-a68f-f9fea8534b97, plan ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 01).
</context>

<scope>
- src/Overmind.Host/Api/Contracts/ClaimSettingsContracts.cs (new): UpdateClaimSettingsRequest(int ClaimStaleThresholdMinutes, string? AgentNamingRules); ClaimSettingsResponse(int ClaimStaleThresholdMinutes, string? AgentNamingRules).
- src/Overmind.Host/Api/ClaimSettingsApi.cs (new): MapGroup("/api") + MapGet/MapPut("projects/{projectId:guid}/claim-settings", …) following PlanningWorkflowApi shape — resolve project or HttpErrors.NotFound; PUT calls project.SetClaimSettings(request.ClaimStaleThresholdMinutes, request.AgentNamingRules), SaveChangesAsync, echo response.
- src/Overmind.Core/Entities/Project.cs: public const int MaxNamingRulesLength = 2000; + guard in SetClaimSettings (ArgumentOutOfRangeException.ThrowIfGreaterThan, mirrors SetManifest :71-79; existing callers pass short/null — safe).
- src/Overmind.Host/Api/Validation/ProjectValidators.cs: UpdateClaimSettingsRequestValidator — threshold >= 0 (message notes 0 disables stale detection); AgentNamingRules MaximumLength(2000) when present.
- src/Overmind.Host/Program.cs: app.MapClaimSettingsApi(); next to MapPlanningWorkflowApi (:165).
- tests/Overmind.Api.Tests/ClaimSettingsApiTests.cs (new): PUT→GET echo; whitespace-only rules → GET null; threshold 0 stored; -1 → 400; 2001 chars → 400; unknown project → 404.
- tests/Overmind.Api.Tests/AgentSessionLockApiTests.cs: extend Onboard_NamingRules_ProjectValueAndGenericHint with an API-driven leg — PUT rules → onboard agentIdentity carries them (the SQL-seeded leg stays as the storage pin).
- tests/Overmind.Core.Tests/Entities/EntityFactoryTests.cs: >2000 chars throws (guard pin).
</scope>

<anti-scope>
- No MCP adapter changes (task 02), no web changes (task 03).
- No schema migration (columns exist since 099); no AgentIdentityDefaults text changes.
- No claim-flow/liveness semantics changes; no ProjectResponse/ProjectsApi changes.
</anti-scope>

<constraints>
- Endpoint shape mirrors PlanningWorkflowApi (group on /api, two routes, ValidationProblem errors).
- Rules text: trim + null-on-whitespace (entity already does); cap 2000 enforced at the validator (400) and the entity guard (ArgumentOutOfRange).
- claimStaleThresholdMinutes >= 0; 0 = stale detection off (099 semantics).
- dotnet-coder rules apply: no comments, English, decomposition; load dotnet-best-practices with this artifact.
</constraints>

<acceptance-criteria>
- [x] PUT/GET /api/projects/{id}/claim-settings round-trip echoes both values.
- [x] Whitespace-only rules persist as null; threshold 0 stores; -1 → 400; 2001 chars → 400; unknown project → 404.
- [x] Onboard agentIdentity carries the saved rules via the API-driven test leg.
- [x] SetClaimSettings throws for rules > 2000 chars (entity pin).
- [x] dotnet build green; targeted Overmind.Api.Tests + Overmind.Core.Tests green.
</acceptance-criteria>

<refs>
- Spec: dc030eba-1f10-4bf2-a68f-f9fea8534b97; Plan: ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 01).
- Pattern: src/Overmind.Host/Api/PlanningWorkflowApi.cs; composition src/Overmind.Host/Program.cs:165; validators src/Overmind.Host/Api/Validation/ProjectValidators.cs; entity src/Overmind.Core/Entities/Project.cs:35/:64-69/:71-79.
- Tests: tests/Overmind.Api.Tests/AgentSessionLockApiTests.cs (Onboard_NamingRules_ProjectValueAndGenericHint).
- Task 03 (order 3) consumes this contract; task 02 (order 2) is independent.
</refs>

<obstacles>
- (none yet — append during execution)
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 01 (batch breakdown; review: light).
- 2026-10-06 [agent] — 121/01 coding round started
- 2026-10-06 [agent] — First Api.Tests run showed 10 failures across ClaimSettingsApiTests + the extended onboard test: requests fell through to the SPA fallback (GET 200 html, PUT 405). Root cause was a stale Overmind.Host binary — the PostToolUse dotnet-build hook raced my own solution build, and the incremental check saw Program.cs as up-to-date against a dll written mid-edit. Code itself was verified correct via an out-of-tree WebApplication route dump (both routes register). After a forced rebuild: Api.Tests 672/672, Core.Tests 367/367, Infrastructure.Tests 692/692, Mcp.Tests 509/509 — all green. Reviewer note: if endpoints 404/405 in a test run, force a Host rebuild before debugging the code.
- 2026-10-06 [agent] — Coding round complete — light review gate. Commit 392568f on main (no push). All 5 AC ticked; full serial test sweep green (Api 672, Core 367, Infrastructure 692, Mcp 509).
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (parity polish: over-limit 400 test could GET-assert non-persistence like its negative-threshold sibling — optional). Pattern fidelity to PlanningWorkflowApi 1:1, validation semantics per contract, auth-consistent, mutation-checked tests (removed cap → 500 caught), spot-checks 9/9 + 31/31 + build clean. Commit 392568f.
</log>