---
feature: 021
task: 01
slug: task-brief
status: done
review: light
created: 2026-09-20
---

# 021/01 — Brief backend contract (entity + migration + Host/MCP)

<goal>
PlanningArtifact gains a typed nullable Brief field exposed additively through both doors: the Host artifacts API (create/update/response + validation) and the MCP planning_upsert_artifact tool. Task-kind-only, 400 elsewhere.
</goal>

<context>
Feature 021-task-brief, layer 1 of 3. Tasks 02 (round-trip) and 03 (web) consume this contract. Spec decision: explicit optional scalar, max 500, no versioning (brief change bumps UpdatedAt only, no body version, no logNote required).
</context>

<scope>
- `src/Overmind.Core/Entities/PlanningArtifact.cs`: nullable `Brief` property + `SetBrief(string?)` mutation following the SetReview pattern (no version bump, UpdatedAt touch).
- EF migration `AddArtifactBrief` (nullable TEXT column on PlanningArtifacts), modeled on `20260919172235_AddArtifactPreservedFrontmatter.cs`; OvermindDbContext config if the sibling columns are configured explicitly.
- `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs` UpsertAsync: kind guard — brief present (non-null) on a non-task kind → PlanningFailureKind.InvalidInput mapped to 400 via PlanningErrors (same door for API and agent routes); apply brief on create (PlanningArtifact.Create gains the parameter or the service sets it after create — follow the least-invasive seam) and on update (null = untouched, empty string = cleared).
- `src/Overmind.Host/Api/Contracts/PlanningContracts.cs`: `CreatePlanningArtifactRequest` + `Brief?`; `UpdatePlanningArtifactRequest` + `Brief?`; `PlanningArtifactResponse` + `Brief` (null for non-tasks). Mapper update in `PlanningMapper.ToArtifactResponse`.
- `src/Overmind.Host/Api/Validation/PlanningValidators.cs`: Brief MaximumLength(500) when not null (create + update validators).
- `src/Overmind.Mcp/OvermindPlanningTools.cs` UpsertArtifact: optional `brief` parameter, included in the posted JSON only when provided; one clause in the tool description.
- Tests: update the pinned posted-JSON contract test in `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs`; Api.Tests coverage — create with brief, update set/clear/untouched, 400 on brief for spec/plan/result, response shape includes brief.
</scope>

<anti-scope>
- No materializer/importer changes (task 02), no web changes (task 03).
- No version rows for brief changes; no logNote requirement.
- No new endpoints or tools — brief rides existing shapes (additive-only).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new NuGet packages; no comments unless non-obvious; English-only.
- dotnet build Overmind.sln + dotnet test Overmind.sln green (record before/after test counts in the log).
</constraints>

<acceptance-criteria>
- [ ] Create a task with brief via POST /api/agent/.../artifacts → stored, returned in response.
- [ ] Update with brief:"new" changes it; brief absent leaves it; brief:"" clears it (response reflects each).
- [ ] brief on spec/plan/result → 400 InvalidInput with a clear message, both API and agent routes.
- [ ] Brief longer than 500 chars → 400 validation error.
- [ ] MCP planning_upsert_artifact with brief posts it; contract test pins the updated key set; brief-only change creates no new version row.
- [ ] dotnet test green incl. new tests.
</acceptance-criteria>

<refs>
- Spec: feature 021-task-brief, spec artifact (validated 2026-09-20) — decisions on update semantics + kind guard.
- Pattern refs: PlanningArtifact.SetReview (mutation shape), 20260919172235_AddArtifactPreservedFrontmatter.cs (migration template), PlanningErrors/PlanningFailureKind (typed 400), PlanningToolContractTests.cs:130 (pinned JSON keys).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — handed to dotnet-coder subagent (session board-clear, 2026-09-20).
- 2026-09-20 — Coder done: Brief field + migration AddArtifactBrief + kind-guard (BriefNotAllowedForKind, 400) + DTOs/validators + MCP brief param; suite 770 → 787 (+17). One conscious deviation flagged for review: UpsertAsync skips UpdateBody when body unchanged and logNote empty (needed for the no-version-row AC; identical re-post no longer creates a version). To review.
- 2026-09-20 — Review PASS-WITH-NITS (audits/review-021-01.md): 0 blockers, 4 NITs (identical re-post now 201 vs 200 — microbatch candidate; whitespace-only brief not trimmed; brief after ct in signature — named-arg call). Coder deviation accepted with justification (no-op re-post no longer creates junk versions). Suite 787/787. Done.
</log>
