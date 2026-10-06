---
feature: 049
slug: backlog-index-retirement
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 049 — Retire BACKLOG.md generated index: состояние — только борда (UI/MCP), на диске — дерево артефактов

Repo root: `/mnt/Ubuntu-Dev/Projects/AI-Tools/Overmind`. Ordering: code -> tests (green) -> on-disk migration on the NEW build -> docs -> sweep. Spec: `cc4c4392-566e-4ff3-a84a-ff6aa61914fd`.

<goal>
Land the writer removal, pin the migration semantics with tests, migrate the two live tracked index rows (this repo + crew kit project) via one normal pass, replace the leftover hand content with the static pointer, and sweep all living in-repo docs — build + full serial test suite green at the end.
</goal>

<scope>
Step 1 — Writer removal (code, 2 files).
- `src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs`: delete `plans.Add(BuildBacklogPlan(root, features));` (~line 74), `BuildBacklogPlan` (527–538), and the BACKLOG.md branch in `HashTrackedDiskCopyAsync` (~665–675; method collapses to plain byte hashing of planned files).
- `src/Overmind.Infrastructure/Planning/ManagedBlock.cs`: delete `Render`, `Merge`, `StatusMarker` (~lines 10–42, 105–111). Keep `StartMarker`/`EndMarker`, `TryExtract`, `Remove` — legacy cleanup.
- KEEP untouched: `BacklogIndexRelativePath` const (line 12) and the block-hash branches in `DetectDriftAsync` (~187), `InspectTrackedCopyAsync` (~313), `RemoveTrackedCopyAsync` (~355), the `ClearAsync` strip path — they own legacy-row cleanup. `AgentFileConflictScanner.cs` line 61 stays.
- Verify: `dotnet build` green; `grep -rn "ManagedBlock.Render\|ManagedBlock.Merge" src/` empty.

Step 2 — Migration + regression tests (`tests/Overmind.Infrastructure.Tests/PlanningMaterializerTests.cs`).
Rework existing index assertions (~lines 77, 173–266, 434–448, 543, 641–676, 827–888, 965, 1017, 1063, 1341–1366, 1539): generation expectations flip to "no BACKLOG.md written, no row tracked". Add migration pins:
  a) legacy tracked row + block in sync -> pass strips the block, hand prose byte-identical, row deleted, subsequent passes never touch the file again;
  b) drifted (hand-edited) block -> refusal, nothing written; `force` -> strip + warning (033 semantics);
  c) file without markers (hand-only) -> row dropped as NotFound, file untouched;
  d) fresh project, strategy on -> no `BACKLOG.md` on disk, no row;
  e) strategy switch archive->off with a legacy archive-root copy -> archive copy deleted, repo prose kept (extends the existing `StrategySwitch_ArchiveToOff_*` trio);
  f) drift detection on a legacy row before any pass -> no false Modified (block-only hash).
- Verify: `dotnet test tests/Overmind.Infrastructure.Tests` green.

Step 3 — Consumer tests rework.
- `tests/Overmind.Infrastructure.Tests/EntryMaterializationTests.cs` (~279): entry-only pass no longer lists the index in Skipped.
- `tests/Overmind.Infrastructure.Tests/BacklogImportMaterializationTests.cs` (~72–89, ~326–332): import x materialization byte-stability without an index.
- `tests/Overmind.Infrastructure.Tests/PlanningRoundTripTests.cs` (~797): snapshot without `BACKLOG.md`.
- `tests/Overmind.Infrastructure.Tests/PlanningServiceGuardTests.cs` (~98): failure simulation via a feature file instead of the index.
- `tests/Overmind.Api.Tests/PlanningMaterializationTests.cs` (~69 keep, ~82–98, ~246, ~281, ~323): refusal detail / run report / stalePaths without the index.
- `tests/Overmind.Api.Tests/OnboardApiTests.cs` (~441): sandbox scenario without index edits.
- `tests/Overmind.Mcp.Tests/Canned.cs` (~323, ~328, ~346): canned materialize/status payloads swap the index entry for feature files (wire shape unchanged).
- KEEP: `CrudRoundtripTests.cs` (~429–500, legacy `FeatureId=null` row shape), `InstallCommandOnboardTests.cs` (scanner fixture).
- Confirm-zero: grep "BACKLOG" in `PlanningFtsSyncTests`, `PlanningSectionMirrorRenderTests`, `PlanningMaterializationStatusReaderTests`, `ProjectMaterializationRunMigrationTests`, `ProjectLastMaterializationSkipsMigrationTests`, `OnboardingToolContractTests`, `PlanningToolContractTests` — expect folder-only refs, no edits.
- Verify: full serial `dotnet test` over `Overmind.sln` green.

Step 4 — On-disk migration (one-time, requires the NEW build).
- Restart the dev Overmind Host on the fresh build (the pass runs in-process; the old binary would still write the index).
- Trigger one pass (`planning_materialize` or a board mutation) on THIS repo's project: expect `BACKLOG.md` untracked, block stripped, prose kept; `planning_materialize_status` shows no index among trackedFiles/written. Repeat path applies to the crew kit project (`src/OvermindCrewKit/BACKLOG.md` -> tombstone header only) on its next pass — no extra work, same machinery.
- Deferred-ledger orphan sweep BEFORE pointer replacement: enumerate open (non-`[x]`, non-tombstoned) lines of the hand content; verify each against board features/entries; promote orphans via `planning_create_feature`/`write_entry`. Known candidates: macOS `.pkg` half, P0 `overmind.localhost` browser-proxy check, zero-byte `overmind.db` boot crash.
- Hand-replace `BACKLOG.md` content with the static pointer (git-tracked edit):
  `# BACKLOG` + `<removed 2026-10-06 — generated index retired (049); board is the only state copy>` + two bullets: `BACKLOG/<feature-slug>/` and `ENTRIES/<type>/` are read-only materialized mirrors (MCP-less sub-agents read here), rewritten by the materializer, never hand-edited + closing line: any status found in a file is stale by definition — check the board.
- Verify: `git diff BACKLOG.md` shows exactly the strip+pointer; no tracked row for it.

Step 5 — Docs sweep (living in-repo surface).
- `README.md`: lines 29, 58 ("live index" wording), 109 (repo layout row) — board is the index.
- `CONTEXT.md`: line 52 (materializer description — drop the managed block, add retirement + legacy strip), line 149 (Materialization bullet — index maintenance out; block-strip stays as legacy cleanup; "block-only for BACKLOG.md" hash note -> legacy context), line 151 (declared losses — index no longer exists), line 170 (hardening nits home -> board/microbatch, not BACKLOG.md Deferred). Line 131 is inside a `<removed>` tombstone comment — DO NOT touch.
- `STRUCTURE.md`: lines 3, 8 (tree), 15 (FEATURELIST description), 42, 100 (reading order).
- `AGENTS.md`: line 36 (work tracking line).
- `TECHNICAL-SOLUTIONS.md`: NEW ADR — "BACKLOG.md index retired: the board is the only state copy" (field case, trade-offs, supersedes the index pointers in earlier ADR consequence lines ~24/92/101/108, which stay untouched per ADR immutability).
- Crew kit x8 (living): `.claude/CLAUDE.md` (~181), `.qwen/QWEN.md` (~182), `.gemini/GEMINI.md` (~179), `.kimi-code/AGENTS.md` (~190) — swap the "BACKLOG.md at the project root is a materialized index..." sentence for: no index file exists; board = UI/MCP; `BACKLOG/` tree = materialized contracts. `overmind-ceremony/SKILL.md` x4 ports (`.claude`, `.kimi-code`, `.qwen`, `.gemini/antigravity`, ~line 40) — same swap.
- Verify: grep sweep (Step 6).

Step 6 — Final gates.
- `dotnet build` green; full serial `dotnet test` green.
- `grep -rn "BACKLOG\.md"` over `src/`, `tests/`, living root docs, kit living files — only intended residues: materializer legacy code + its tests, scanner + fixture, kit tombstone header, FEATURELIST history, materialized trees.
- Checkpoint commit per crew rule 6 (conventional, attributed; no push).
</scope>

<anti-scope>
- No changes to `BACKLOG/` tree semantics, importer, onboard block writer, `AgentFileConflictScanner`, web UI, MCP tool docs, FEATURELIST, ENTRIES/, materialized trees, other repos, deployed kit copies.
- No schema/migration changes; no new NuGet dependencies.
- No feature-status moves and no notifications from the executing agent — orchestrator owns ceremony moves.
</anti-scope>

<constraints>
- Migration must run on the new build (Host restart), never the old binary.
- 033 guard semantics preserved and pinned by tests before the live migration runs.
- Hand content outside the markers survives byte-for-byte; ledger orphans promoted before the pointer edit; the pointer edit is a hand git-tracked change, reviewable in the diff.
- Code style: no comments for obvious code, English-only messages, no `region`; match surrounding human style.
</constraints>

<acceptance-criteria>
- [ ] Steps 1–2: writer gone, `dotnet build` + Infrastructure suite green, migration semantics pinned (a–f).
- [ ] Step 3: full serial `dotnet test` green; kept tests (CrudRoundtrip, InstallCommandOnboard) untouched.
- [ ] Step 4: live pass untracks and strips both index files; pointer file in place; ledger orphans have board homes (or an explicit obstacles note why one stayed).
- [ ] Step 5: all listed docs updated; CONTEXT:131 tombstone comment untouched; new ADR present.
- [ ] Step 6: build + tests green, grep sweep clean of unintended references.
</acceptance-criteria>

<refs>
- Spec: cc4c4392-566e-4ff3-a84a-ff6aa61914fd (049-backlog-index-retirement) — decisions and human flags live there.
- Code: src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs, src/Overmind.Infrastructure/Planning/ManagedBlock.cs.
- 033-materializer-clobber-guard, 042-onboard-observability, 075-materialization-git-hygiene, 116-crewkit-in-repo, 125-microbatch-cleanup.
</refs>

<obstacles>
- The executing agent must NOT touch spec/plan bodies (frozen once the human validates); discovered quirks go here via planning_patch_artifact obstacles/log only.
- The step-4 live migration mutates this very repo's working tree while the feature is in flight — expect `BACKLOG.md` and kit file diffs to mix with ceremony diffs in git status; the checkpoint commit should separate them (code+tests vs migration+docs) for reviewability.
- If the live pass refuses on a drifted block (someone hand-edited the index since the last write), do NOT force silently: report the diff to the human; force is their call (kept-foreign warning path).
</obstacles>

<log>
- 2026-10-06 [agent] — plan drafted: 6 steps, exact files/lines from code research, migration ordering (new build first), docs/kit x8 enumerated
- 2026-10-06 [agent] — validated with human proof: notification 54d89f79-6f81-41eb-a538-e1735aa8eef4 replied 2026-10-06 13:05:12 UTC
</log>
