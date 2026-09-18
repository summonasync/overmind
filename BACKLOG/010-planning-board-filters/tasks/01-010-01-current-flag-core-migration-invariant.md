---
feature: 010
task: 01
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/01 — Current flag: Core + migration + invariant

<goal>
PlanningFeature has an IsCurrent pin flag persisted in SQLite, and PlanningFeatureService exposes SetCurrentAsync enforcing "at most one current feature per project" atomically. All existing behavior unchanged.
</goal>

<context>
Feature 010 adds a CURRENT pin to planning features (user-facing: "the feature we are working on right now"). This task is the backend foundation; the API/MCP doors (task 02) and web UI (tasks 03-04) build on it. The executing subagent reads ONLY this file.
</context>

<scope>
- src/Overmind.Core/Entities/PlanningFeature.cs — new `bool IsCurrent { get; private set; }` (default false), a `SetCurrent(bool isCurrent)` method (sets flag + UpdatedAt), and Restore(...) gains the flag (keep import paths working)
- src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs — property config, default false
- New EF migration: `dotnet ef migrations add AddPlanningFeatureIsCurrent --project src/Overmind.Infrastructure` (design-time factory exists; do NOT hand-edit generated files)
- src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs — `SetCurrentAsync(Guid projectId, string slug, bool isCurrent, CancellationToken)`: load target feature (missing → same PlanningException kind DeleteAsync uses), when isCurrent=true clear IsCurrent on all OTHER features of the same project in the same SaveChanges; when false just clear the target
- Ripples: fix every call site of PlanningFeature.Restore (BacklogFeatureImport.cs and any tests) to pass isCurrent: false
- tests/Overmind.Infrastructure.Tests — new tests: pin sets flag; pinning a second feature unpins the first (same project); unpin clears; two different projects keep independent currents
</scope>

<anti-scope>
- No HTTP API, no MCP, no web changes (tasks 02-04)
- No automatic status transitions (pinning does NOT touch Status)
- No changes to PlanningArtifact or artifact statuses
</anti-scope>

<constraints>
- .NET 10 / C# 14, EF Core + SQLite; no new NuGet packages
- No comments in code; match existing style (file-scoped namespaces, expression style already used)
- Run `dotnet build Overmind.sln` then `dotnet test Overmind.sln --no-build` — both must be green. Known flake: `Internal CLR error 0x80131506` on cold dotnet invocations → `dotnet build-server shutdown` and retry
</constraints>

<acceptance-criteria>
- [ ] IsCurrent persisted (migration applies cleanly, column default false)
- [ ] SetCurrentAsync(pin) on feature B clears a previously pinned feature A of the same project in one SaveChanges
- [ ] SetCurrentAsync on a missing feature throws the same PlanningException kind as DeleteAsync
- [ ] Cross-project isolation pinned by a test
- [ ] dotnet build + dotnet test green (baseline was 544/544 before this feature)
</acceptance-criteria>

<refs>
- @src/Overmind.Core/Enums/PlanningFeatureStatus.cs — existing status enum (untouched)
- @src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs — CreateAsync/DeleteAsync patterns to mirror
- @src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs — PlanningFeature config block ~line 143
- tasks/02-current-api-mcp.md — depends on this task
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Started: Core IsCurrent flag + migration + single-current invariant
- 2026-09-18 — Done: IsCurrent flag + migration 20260918113608 + SetCurrentAsync invariant; 552/552 tests green (baseline 544 + 8 new)
</log>
