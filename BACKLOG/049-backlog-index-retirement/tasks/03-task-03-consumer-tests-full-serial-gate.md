---
feature: 049
task: 03
slug: backlog-index-retirement
status: done
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
- [x] All listed files reworked; kept files untouched.
- [x] Full serial `dotnet test` green.
- [x] Confirm-zero grep outcome recorded in obstacles.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 3.
</refs>

<obstacles>
- Confirm-zero grep: BACKLOG refs in PlanningFtsSyncTests, PlanningSectionMirrorRenderTests, PlanningMaterializationStatusReaderTests (folder-path refs only), ProjectMaterializationRunMigrationTests, ProjectLastMaterializationSkipsMigrationTests, OnboardingToolContractTests, PlanningToolContractTests — all folder-only (`BACKLOG/...`, `/repo/BACKLOG`); zero edits, as the scope expected.
- Scope discrepancy resolved in favor of the task-01 red list: the scope's confirm-zero list named PlanningMaterializationStatusReaderTests as no-edit, but task 01's obstacles pin 2 reds there (count assertions 5→4). Both tests reworked accordingly.
- Api `Materialize_EverythingGuarded_Returns409ListingRefusalsInDetail`: plain index-drop would have collapsed it into a near-duplicate of `Materialize_WithoutForce_RefusesForeignFileAndWritesNothing`. Kept its identity by upgrading the failure simulation to the OnboardApiTests pattern: hand-edited tracked feature.md (refused: local edits) + foreign squatter on a second planned feature's path (refused: not tracked). Detail asserts both paths, "nothing was written" and the planning_upsert_artifact hint.
- OnboardApiTests `Onboard_MaterializationBlock_FailedSync_CarriesErrorAndClearsAfterRecovery`: the pre-existing duplicated restore pair (featureDoc + indexPath written twice in a row) collapsed into a single featureDoc restore — the duplication existed only to mirror the index restore.
- Kept untouched as contracted: CrudRoundtripTests (legacy `FeatureId=null` row shape stays legal) and InstallCommandOnboardTests (AgentFileConflictScanner fixture; scanner keeps its BACKLOG.md line check).
- Gates: dotnet build 0 warnings / 0 errors; serial (-m:1) Core 365/365, Infrastructure 692/692 (all 14 reds flipped), Api 663/663 (all 5 reds flipped), Mcp 509/509. Commit 8905e1d — the 8 test files only, no production changes, no push.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 3
- 2026-10-06 [agent] — 049/03 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (Canned fixture now self-contradictory: spec.md in both written and stalePaths — inert, no cross-field consumers; filed to ledger #264). 3 reworked tests compared against parent — no weakening, EverythingGuarded upgrade verified, keep-untouched files intact, serial runs Infra 692/692 + Api 663/663 confirmed. Commit 8905e1d.
</log>