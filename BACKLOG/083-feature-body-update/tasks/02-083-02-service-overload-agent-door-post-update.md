---
feature: 083
task: 02
slug: feature-body-update
status: done
review: light
created: 2026-09-27
brief: Service overload (logNote+source, no status) + agent POST /update route, validator, 070 echo, queue-tail pin
---

# 083/02 — service overload + agent-door POST /update

<goal>
The agent door gets a partial feature-edit endpoint: `POST /api/agent/projects/{projectId:guid}/planning/features/{slug}/update` — title/body/priority, mandatory logNote, `[agent]` provenance, 070-family echo.
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 01 (guard + sourced Update) landed. Service: src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs:64-109 — existing `UpdateAsync(projectId, slug, title?, body?, status?, priority?)` (project/feature resolution, transaction, `AssignQueueTailAsync` on priority change, materializer sync; status branch hardcodes [ui] — irrelevant here). Agent door patterns: src/Overmind.Host/Api/AgentPlanningApi.cs (POST + verb routes: status/current/queue-move; error mapping via PlanningErrors.Map). Echo family: 070 slim mutations — `{ slug, status, version, updatedAt, bodyChars }` (see how artifact mutations echo). Validators: FluentValidation request records in src/Overmind.Host/Api/Contracts (see SetPlanningArtifactStatusRequest / update requests).
</context>

<scope>
- Infrastructure: `PlanningFeatureService.UpdateAsync` overload — `(Guid projectId, string slug, string? title, string? body, FeaturePriority? priority, string logNote, PlanningLogSource source, CancellationToken ct)` — logNote mandatory (throw PlanningException on null/whitespace — reuse an existing failure kind if one fits, else the validator catches it first), NO status parameter, calls the entity's sourced `Update`, priority → `SetPriority` + `AssignQueueTailAsync` (072 semantics), transaction + sync as today. Existing human-door `UpdateAsync` signature unchanged (PUT keeps working, guard applies via entity).
- Host contracts: `UpdatePlanningFeatureByAgentRequest` (title?, body?, priority?, logNote) + FluentValidation: logNote NotEmpty; at least one of title/body/priority present; title/body non-empty whitespace-free when present; priority must parse to FeaturePriority (case-insensitive p0-p3) — invalid → 400 listing allowed values.
- Host route: `group.MapPost("/projects/{projectId:guid}/planning/features/{slug}/update", UpdateFeature)` in AgentPlanningApi's MapAgentPlanningApi; handler resolves via the service overload with `PlanningLogSource.Agent`; response echo `{ slug, status, version, updatedAt, bodyChars }`; unknown project/feature → existing PlanningException mapping (404); guard violation → LogHistoryNotAppendable (already mapped since 078).
- Tests (tests/Overmind.Api.Tests/ — follow AgentPlanningApiTests patterns): happy title-only / body-only / priority-only (priority test ALSO asserts queue-tail reassignment), all-fields-empty → 400 naming editable fields, missing logNote → 400, bad priority → 400 listing p0-p3, unknown slug → 404, hand-crafted log rewrite in body → 409-family failure, response echo shape + `[agent]` marker present in the feature's log afterwards.
</scope>

<anti-scope>
- No Mcp changes (task 03), no Core changes (task 01 owns the entity), no web, no human-door behavior change beyond the entity guard (logNote NOT required on PUT).
</anti-scope>

<constraints>
- No comments; English strings; no new packages; NO git commits.
- `dotnet build Overmind.sln` 0W/0E; full `dotnet test` green (1533 + task 01 additions).
- Diff confined to src/Overmind.Infrastructure, src/Overmind.Host + tests/Overmind.Api.Tests.
</constraints>

<acceptance-criteria>
- [x] POST .../features/{slug}/update edits title/body/priority partially; echo {slug,status,version,updatedAt,bodyChars}; no body echo.
- [x] Missing logNote or all-empty request → 400 with a specific reason; bad priority → 400 listing p0-p3.
- [x] Priority change reassigns queue tail (072) — asserted in a test.
- [x] Unknown slug → 404; hand-crafted log history rewrite → LogHistoryNotAppendable surfaced.
- [x] Feature log carries the `[agent]` sourced line after an agent edit; human PUT still works without logNote.
- [x] dotnet build 0W/0E + full suite green; diff confined to Infrastructure/Host/Api tests.
</acceptance-criteria>

<refs>
- src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs:64-109 (existing UpdateAsync)
- src/Overmind.Host/Api/AgentPlanningApi.cs (route + handler patterns)
- src/Overmind.Host/Api/Contracts (request records + validators)
- PlanningException.LogHistoryNotAppendable mapping (078)
- tests/Overmind.Api.Tests/AgentPlanningApiTests.cs
</refs>

<obstacles>
<obstacles>
- 2026-09-27 [083/02] Scope wording said the hand-crafted log rewrite returns "409-family"; the existing 078 mapping routes LogHistoryNotAppendable (PlanningFailureKind.InvalidInput) to 400 with the typed ProblemDetails title. No behavior changed in this task — the test asserts 400 + title "LogHistoryNotAppendable", which satisfies the AC ("LogHistoryNotAppendable surfaced"). Flag for the reviewer in case the 409 mapping itself should change in a follow-up.
</obstacles>
</obstacles>

<log>
- 2026-09-27 — Task created from validated plan 19f231e5; dispatch after task 01.
- 2026-09-27 [agent] — Implemented agent-door feature update: PlanningFeatureService.UpdateAsync overload (logNote mandatory via FeatureLogNoteRequired, no status param, sourced entity Update, SetPriority+AssignQueueTail in transaction + materializer sync); UpdatePlanningFeatureByAgentRequest contract + FluentValidation (logNote required, at-least-one-field, whitespace guards, priority parse with p0-p3 list); POST /api/agent/projects/{projectId}/planning/features/{slug}/update route + handler with {slug,status,version,updatedAt,bodyChars} echo; DI registration. 11 new test cases in AgentPlanningApiTests (all green). dotnet build 0W/0E, full suite 1554/1554 (baseline 1543 + 11), dotnet format clean on touched files. Diff confined to Infrastructure/Host/Api.Tests. All 6 ACs verified and ticked; not closing — ready for review.
- 2026-09-27 [agent] — Implementation complete, all 6 ACs ticked after real verification (build 0W/0E, 1554/1554 tests, format clean) — moving to review per task 083 pipeline; reviewer to run review-083-02.
- 2026-09-27 [agent] — light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 4 SUGGESTION microbatch) — audits/review-083-02.md; независимые гейты: build 0W/0E, 1554/1554; obstacle 400-vs-409 принят с обоснованием (спека противоречила shipped 078-маппингу, тест пинит факт); human PUT и импортёр не тронуты; done by orchestrator after review
</log>