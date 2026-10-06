---
feature: 049
task: 03
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
depends_on: 01
brief: Flip consumer tests off the index (Entry/Import/RoundTrip/Guard/Api/Mcp Canned), keep CrudRoundtrip + InstallCommandOnboard, full serial dotnet test green.
---

# Task 03 — Consumer tests + full serial gate

<goal>
Every test outside PlanningMaterializerTests that pins index behavior flips to the no-index world, and the full serial suite goes green.
</goal>

<scope>
- `tests/Overmind.Infrastructure.Tests/EntryMaterializationTests.cs` (~279): entry-only pass no longer lists the index in Skipped.
- `tests/Overmind.Infrastructure.Tests/BacklogImportMaterializationTests.cs` (~72–89, ~326–332): import x materialization byte-stability without an index.
- `tests/Overmind.Infrastructure.Tests/PlanningRoundTripTests.cs` (~797): snapshot without BACKLOG.md.
- `tests/Overmind.Infrastructure.Tests/PlanningServiceGuardTests.cs` (~98): failure simulation via a feature file instead of the index.
- `tests/Overmind.Api.Tests/PlanningMaterializationTests.cs` (~69 stays true, ~82–98, ~246, ~281, ~323): refusal detail / run report / stalePaths without the index.
- `tests/Overmind.Api.Tests/OnboardApiTests.cs` (~441): sandbox scenario without index edits.
- `tests/Overmind.Mcp.Tests/Canned.cs` (~323, ~328, ~346): canned materialize/status payloads swap the index entry for feature files — wire shape unchanged.
- KEEP untouched: `CrudRoundtripTests.cs` (~429–500, legacy `FeatureId=null` row shape stays legal) and `InstallCommandOnboardTests.cs` (AgentFileConflictScanner fixture — the scanner stays).
- Confirm-zero: grep BACKLOG in `PlanningFtsSyncTests`, `PlanningSectionMirrorRenderTests`, `PlanningMaterializationStatusReaderTests`, `ProjectMaterializationRunMigrationTests`, `ProjectLastMaterializationSkipsMigrationTests`, `OnboardingToolContractTests`, `PlanningToolContractTests` — expect folder-only refs, no edits; note the result in obstacles.
</scope>

<anti-scope>
Production code; PlanningMaterializerTests (task 02); fixture text of the conflict scanner.
</anti-scope>

<constraints>
Full serial `dotnet test` over `Overmind.sln` must pass (Core/Infrastructure/Api/Mcp) before handoff.
</constraints>

<acceptance-criteria>
- [ ] All listed files reworked; kept files untouched.
- [ ] Full serial `dotnet test` green.
- [ ] Confirm-zero grep outcome recorded in obstacles.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 3.
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 3
</log>