---
feature: 049
task: 02
slug: backlog-index-retirement
status: done
review: light
created: 2026-10-06
depends_on: 01
brief: Rework PlanningMaterializerTests index pins and add migration pins a–f: strip keeps prose, drift refuses, force warns, no-markers NotFound, fresh project no index, archive switch, no false drift.
---

# Task 02 — Migration + regression pins

<goal>
PlanningMaterializerTests stops pinning index generation and pins the new contract: no pass creates the index; a legacy tracked row migrates on the next pass with 033 semantics intact.
</goal>

<scope>
File: `tests/Overmind.Infrastructure.Tests/PlanningMaterializerTests.cs`. Rework existing index assertions (~lines 77, 173–266, 434–448, 543, 641–676, 827–888, 965, 1017, 1063, 1341–1366, 1539): generation expectations flip to "no BACKLOG.md written, no row tracked". Add migration pins:
a) legacy tracked row + block in sync -> the pass strips the block, hand prose stays byte-identical, row deleted, subsequent passes never touch the file again;
b) drifted (hand-edited) block -> refusal with nothing written; with `force` -> strip + per-file warning (033 kept-foreign path);
c) file without markers (hand-only content) -> row dropped as NotFound, file untouched;
d) fresh project, strategy on -> no BACKLOG.md on disk, no tracked row;
e) strategy switch archive->off with a legacy archive-root copy -> archive copy deleted, repo prose kept (extends the existing `StrategySwitch_ArchiveToOff_*` trio);
f) drift detection on a legacy row before any pass -> no false Modified (the row hash covers the block only).
</scope>

<anti-scope>
Consumer test files (task 03 owns them); production code beyond what task 01 already changed; schema.
</anti-scope>

<constraints>
Tests describe behavior, not implementation details; keep the file's existing helper/naming style; `dotnet test tests/Overmind.Infrastructure.Tests` green before handoff.
</constraints>

<acceptance-criteria>
- [x] All generation pins reworked; suite green.
- [x] Pins a–f present and passing.
- [x] No test asserts the index is created or refreshed anywhere.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 2; 033-materializer-clobber-guard.
</refs>

<obstacles>
- Pin (b) force semantics — task body said "strip + per-file warning", but the post-removal code path (and the plan's own obstacles wording "kept-foreign warning path") does NOT strip on force: GuardStaleTrackedFilesAsync passes on force, then RemoveTrackedCopyAsync hits the drifted block, warns "Kept foreign file at a tracked path: {abs}", returns KeptForeign — file bytes kept, row retained (so drift keeps reporting Modified). Strip only happens for an UNdrifted block (pin a). Spec constraint "force strips with a warning" does not match shipped behavior; the test pins actual behavior (refusal without force, kept-foreign warning with force). If strip-on-force is wanted, that is a production change outside this task's anti-scope.
- The task constraint "dotnet test tests/Overmind.Infrastructure.Tests green" is not reachable inside task 02 alone: 14 expected-reds remain in files owned by task 03 (BacklogImportMaterializationTests 3, EntryMaterializationTests 1, PlanningMaterializationStatusReaderTests 2, PlanningRoundTripTests 7, PlanningServiceGuardTests 1) — exactly the task 01 expected-red inventory minus this class. Assembly: 678 passed / 14 failed; PlanningMaterializerTests: 43/43.
- Strategy-switch tests (OnToArchive, WritePhaseFailure, Off) seed the legacy index row directly after the first On sync — generation no longer produces it, so the row+file pair simulates the pre-removal world deterministically.
- Sync_BacklogEditOutsideManagedBlock and Sync_SingleRefusedIndex were rewritten into legacy pins (prose-edit-never-blocks-strip, block-edit refuse/force); On_ReviewFeature dropped its tilde-marker half (index-only concept, renamed On_ReviewFeature_EmitsReviewStatus). Test count in the class unchanged: 43.
- Shared helpers added at the file bottom: LegacyIndexBlock const, LegacyIndexFile(prefix, block), SeedTrackedRowAsync(ctx, projectId, featureId, path, sha).
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 2
- 2026-10-06 [agent] — 049/02 coding round started
- 2026-10-06 [agent] — 049/02: 25 generation pins reworked; class 43/43 green (was 18/43). Commit d063aea.
- 2026-10-06 [agent] — 049/02: pins a-f landed and passing — a=Sync_LegacyIndexRow_StripsBlockKeepsProseByteIdenticalAndNeverTouchesTheFileAgain, b=Sync_LegacyIndexBlockHandEdited_RefusesThenForceKeepsForeignWithWarning, c=Sync_LegacyIndexRowWithoutMarkers_DropsRowAndKeepsHandFileUntouched, d=Sync_FreshProject_WritesNoIndexAndNoRow (+On_WritesFullLayout asserts no index), e=StrategySwitch_ArchiveToOff_LegacyArchiveIndexCopyDeletedAndRepoProseKept, f=Drift_LegacyIndexRowIsScopedToBlockOnly.
- 2026-10-06 [agent] — 049/02: grep sweep over the file — every remaining BACKLOG.md reference is a legacy-row seed, a relative-path drift assertion, or a ShouldBeFalse absence pin; zero creation/refresh expectations.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (strip-on-force for legacy index rows is shipped non-goal behavior: KeptForeign + row retained forever; production follow-up candidate filed to ledger #263, human decides). Force-pin judged honest to production, mutation-check kills pin (d) on writer-removal revert, spot-comparison vs pre-removal pins showed strengthening not weakening, 43/43 spot-run. Commit d063aea.
</log>