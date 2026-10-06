---
feature: 049
task: 01
slug: backlog-index-retirement
status: done
review: light
created: 2026-10-06
brief: Remove the BACKLOG.md writer: BuildBacklogPlan + plans.Add + HashTrackedDiskCopyAsync branch in PlanningMaterializer.cs; trim ManagedBlock Render/Merge/StatusMarker; legacy cleanup branches stay byte-exact.
---

# Task 01 — Writer removal

<goal>
The materializer stops planning BACKLOG.md: the generated-index writer is removed, the legacy cleanup machinery stays byte-exact.
</goal>

<scope>
- `src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs`: delete `plans.Add(BuildBacklogPlan(root, features));` (~line 74), the whole `BuildBacklogPlan` method (~527–538), and the BACKLOG.md branch in `HashTrackedDiskCopyAsync` (~665–675 — the method collapses to plain byte hashing of planned files).
- `src/Overmind.Infrastructure/Planning/ManagedBlock.cs`: delete `Render`, `Merge`, `StatusMarker` (~lines 10–42 and 105–111). KEEP `StartMarker`, `EndMarker`, `TryExtract`, `Remove`.
- KEEP untouched: the `BacklogIndexRelativePath` const (line 12) and the block-hash branches in `DetectDriftAsync` (~187), `InspectTrackedCopyAsync` (~313), `RemoveTrackedCopyAsync` (~355), the `ClearAsync` strip path, and `AgentFileConflictScanner.cs` (~61).
- Awareness note: after removal `FilePlan.Sha256` is always the content hash (the only `with { Sha256 = ... }` override dies with `BuildBacklogPlan`) — no further code change needed.
</scope>

<anti-scope>
Any behavior change to guards, drift detection, clear, or archive paths; the importer; the onboard block writer; `AgentFileConflictScanner`; any test file (task 02 owns tests).
</anti-scope>

<constraints>
No comments for obvious code; match the file's existing style; `dotnet build` green before handoff.
</constraints>

<acceptance-criteria>
- [x] `dotnet build` green.
- [x] `grep -rn "ManagedBlock.Render\|ManagedBlock.Merge" src/` returns nothing.
- [x] git diff shows deletions only in the listed regions; all legacy cleanup branches (Detect/Inspect/Remove/Clear special cases) present and unchanged.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 1; 033-materializer-clobber-guard (the semantics that must survive untouched).
</refs>

<obstacles>
Expected-red inventory after the writer removal (test files are tasks 02/03 territory, untouched here). Verified green: `dotnet build` 0 warnings / 0 errors; Core.Tests 365/365; Mcp.Tests 509/509. Infrastructure: 653 passed / 39 failed. Api.Tests: 658 passed / 5 failed.

All failures share one root cause: tests read or assert BACKLOG.md, which the materializer no longer writes (`FileNotFoundException: .../BACKLOG.md`, or Shouldly assertions on BACKLOG.md rows/skips/stale paths). Guard logic itself is intact. The two non-FileNotFound Api failures were confirmed green on clean HEAD 5c336b3 via a throwaway worktree, so all 5 Api reds are consequences of this removal, not pre-existing.

Infrastructure (39):
- BacklogImportMaterializationTests (3): Import_OnProject_RowShasMatchFileBytesOnDisk, Import_ReImportAfterDiskEdit_RefreshesRowShaAndKeepsSyncClean, Import_TracksSourceFiles_FirstMaterializeIsByteStableNoOpWithoutForce
- EntryMaterializationTests (1): Sync_HandEditedTrackedEntryFile_RefusesAndWritesNothing
- PlanningMaterializationStatusReaderTests (2): Read_AfterCleanPass_ReportsSuccessRunWithoutStalePaths, Read_PlanPhaseFailureAfterSuccess_RendersNoReportForTheAttempt
- PlanningMaterializerTests (25): Clear_UndeletableFile_SurfacesWarningAndDropsRow, Drift_BacklogIsScopedToBlockOnly, Off_ClearsFilesPrunesDirectoriesRemovesBlockAndDropsRows, On_ReviewFeature_EmitsReviewStatusAndTildeMarker, On_WritesFullLayoutWithNamingAndFrontmatter, Resync_PreservesForeignContentOutsideBlock_ByteForByte, StrategySwitch_ArchiveToOff_CleanRepo_ClearsEverything, StrategySwitch_ArchiveToOff_RemovesArchiveIndexAndKeepsForeignRepoFiles, StrategySwitch_ArchiveToOn_WithForeignRepoFile_RefusesThenForceAdopts, StrategySwitch_OnToArchive_ClearsRepoCopy, StrategySwitch_WritePhaseFailure_OtherRootRowsDropAndPlannedRowsStayOut, Sync_AllFilesRefused_AbortsAndStampsAttemptWithoutSuccess, Sync_AppendsBlockWhenMarkersAbsent, Sync_ArchiveMode_ForeignRepoFileAtTrackedPath_IsKeptWithWarning, Sync_BacklogEditOutsideManagedBlock_NeverBlocks_InsideBlockEdit_Refuses, Sync_CreatesBacklogFileWhenMissing, Sync_DeletedDocument_SpecFileLeavesFeatureFolderIntact, Sync_FailedRunThenSuccess_SplitsAttemptFromSuccessStamps, Sync_RerunWithoutBoardChanges_SkipsEverythingAndStampsSuccess, Sync_RunReportJson_RoundTripsPerFileOutcomes, Sync_SingleRefusedIndex_OffendingPathStaysRelative, Sync_Success_StampsAttemptLastMaterializedAndRunReport, Sync_TrackedFileHandEdited_ForceOverwritesAndWarnsPerFile, Sync_TrackedFilesHandEdited_RefusesWholeRunAndKeepsBytes, Sync_WritePhaseIoFailure_PersistsAttemptStampAndAbortedReport
- PlanningRoundTripTests (7): Import_InlineSections_CanonicalizeOnStore_AndReImportStaysNoOp, Import_NonSortedDependsOn_RestoresFileOrderAndReExportIsStable, RoundTrip_BriefedTask_SurvivesWipeAndReImportAsTypedField, RoundTrip_DependencyLinks_ExportThenImportIntoFreshDatabase_RestoresLinks, RoundTrip_MaterializeIsByteIdempotentAndReImportIsNoOp, RoundTrip_Priority_SurvivesMaterializeWipeAndReImport, RoundTrip_TaskDependencyLinks_ExportThenImportIntoFreshDatabase_RestoresLinks
- PlanningServiceGuardTests (1): Sync_AllSkippedThroughGuard_PersistsErrorStateUntilNextSuccess

Api.Tests (5):
- OnboardApiTests (1): Onboard_MaterializationBlock_FailedSync_CarriesErrorAndClearsAfterRecovery
- PlanningMaterializationTests (4): ArtifactWrite_InOnMode_MaterializesFile, MaterializationStatus_AfterCleanPass_ReturnsCamelCaseStructuredMirrorState, MaterializationStatus_RefusedRun_TreatsEveryTrackedFileAsUnhandled, Materialize_EverythingGuarded_Returns409ListingRefusalsInDetail

Notes:
- AC-2 grep caveat: the literal pattern `ManagedBlock.Render\|ManagedBlock.Merge` substring-matches `OnboardManagedBlock.Merge` in src/Overmind.Mcp/Registration/OnboardBlockWriter.cs (anti-scope class, byte-untouched). Word-boundary check `\bManagedBlock\.(Render|Merge)` returns nothing under src/.
- Awareness item confirmed: after removal FilePlan.Sha256 is always the content hash; the only `with { Sha256 = ... }` override died with BuildBacklogPlan.
- HashTrackedDiskCopyAsync keeps its `string?` signature so GuardLocalEditsAsync (guard code, anti-scope) stays byte-identical; the null path is simply unreachable now.
- Commit 4470663 staged only the two src files; working tree mirror churn from other features left untouched; no push.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 1
- 2026-10-06 [agent] — 049/01 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate; expected-red tests enumerated for 02/03
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (dead using removal formally outside the six named regions — forced consequence of the Merge removal, acceptable). Pure-removal verified (67/0), anti-scope intact byte-wise, null-path claim confirmed, 3 expected-red tests verified to redden for the right reason. Commit 4470663.
</log>