---
feature: 049
slug: backlog-index-retirement
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 049 — Retire BACKLOG.md generated index: состояние — только борда (UI/MCP), на диске — дерево артефактов

<goal>
`BACKLOG.md` at the project root is a second copy of the board in a file, rotting by design: a materialization pass can silently fail (field case DMT) and the index lies while the board is right. No audience needs it — MCP agents get `planning_list_features` (fresher, cheaper), humans have the board UI. This feature removes the GENERATED INDEX, not materialization: the `BACKLOG/nnn-slug/` tree stays (MCP-less sub-agents read materialized `tasks/NN-*.md`, git carries ceremony diffs, external reviewers see contracts next to code). After retirement the board (DB) is the only state copy; disk holds artifact mirrors only.
</goal>

<scope>
1. Writer removal. `PlanningMaterializer` (src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs — the writer lives in Infrastructure, not Host) stops planning `BACKLOG.md`: delete `BuildBacklogPlan` and its `plans.Add(...)` call, and the BACKLOG.md branch in `HashTrackedDiskCopyAsync` (planned-path only; dead once no plan carries that path). The `FilePlan` hash override (`with { Sha256 = ContentHash.Sha256(block) }`) dies with it — after this the plan hash is always the content hash.
2. Legacy-row cleanup kept. The removal-side special cases stay: `BacklogIndexRelativePath` const and the block-hash branches in `DetectDriftAsync`, `InspectTrackedCopyAsync`, `RemoveTrackedCopyAsync` (repo root: strip the managed block via `ManagedBlock.Remove`, byte-preserving hand content outside the markers; archive root: delete the file), and the `ClearAsync` strip path. Existing tracked `BACKLOG.md` rows therefore migrate on the next pass with zero migration script: the row goes stale, the stale guard still refuses a drifted (hand-edited) block without `force` (033 semantics), and the strip keeps hand prose. The drift branch must stay so legacy rows do not false-alarm as Modified between deploy and first pass (the row hash covers the block only).
3. Dead code trim. `ManagedBlock.Render`, `ManagedBlock.Merge`, `StatusMarker` become unused (only `BuildBacklogPlan` called them) — delete. `TryExtract`/`Remove`/markers stay for the legacy cleanup. The onboard managed block (`OnboardManagedBlock`/`OnboardBlockWriter` in Overmind.Mcp.Registration) is a separate machinery — untouched.
4. Existing file fate (DECISION, flagged in obstacles): after the automatic strip, the leftover hand-written content (repo root: legacy hand index header + fossil Order section + Deferred findings ledger, ~89 lines; crew kit root: the 2026-09-24 tombstone header) is replaced by a one-time hand edit with a short static pointer file: board = UI/MCP for state; `BACKLOG/<slug>/` + `ENTRIES/` = read-only materialized mirrors; any status in a file is stale by definition. Before the replacement, an orphan sweep verifies every still-open Deferred line has a board home (feature, microbatch entry); orphans are promoted first — never silently dropped. Git history keeps the full pre-retirement content.
5. Tests. Rework every test pinning index generation (PlanningMaterializerTests, EntryMaterializationTests, BacklogImportMaterializationTests, PlanningRoundTripTests, PlanningServiceGuardTests, Api PlanningMaterializationTests, OnboardApiTests, Mcp Canned.cs) and pin the new contract: no pass creates `BACKLOG.md`; legacy tracked row strips-and-untracks on the next pass; drift/force/kept-foreign semantics unchanged. Keep CrudRoundtripTests (legacy `FeatureId=null` row shape stays legal) and InstallCommandOnboardTests (AgentFileConflictScanner fixture — the scanner stays and still flags agent files that tell agents to pick work from `BACKLOG.md`).
6. Docs (in-repo living surface only): README.md, CONTEXT.md, STRUCTURE.md, AGENTS.md describe board-only state; a new ADR in TECHNICAL-SOLUTIONS.md records the retirement decision (old ADR lines stay untouched per ADR immutability — the new ADR notes it supersedes their `BACKLOG.md` pointers); crew kit living files x8 (4 contract files + overmind-ceremony SKILL.md x4 ports) swap the "materialized index" sentence for the no-index rule.
</scope>

<anti-scope>
- The `BACKLOG/nnn-slug/` tree: content, naming, tracking, drift — unchanged.
- The import pipeline (`BacklogImporter`, `--import-backlog`, planning_import_feature): unchanged; it never read the index ("not read back" stays a declared loss, now trivially).
- Materialization itself: strategies off/on/archive, guards, atomicity, entries projection, run reports — unchanged except the index plan disappearing.
- Onboard managed block writer and `AgentFileConflictScanner` — unchanged.
- FEATURELIST/* (dated evidence snapshots), ENTRIES/, kit historical artifacts under BACKLOG/ — history, not edited.
- Other projects' own AGENTS.md files (Squeezr etc.) and deployed kit copies under `~/.claude` and sibling runtime homes — outside this repo; the human re-syncs (see obstacles).
- Web UI and MCP tool descriptions (they already describe only the `BACKLOG/` tree).
</anti-scope>

<constraints>
- 033 clobber-guard semantics fully preserved for the legacy index row: drifted block refuses the pass, `force` strips with a per-file warning, foreign content at the tracked path is kept and surfaced as a warning.
- No schema change, no one-time migration script, no destructive git: the migration rides the existing stale-row machinery.
- The strip is byte-preserving for hand content outside the markers (existing `ManagedBlock.Remove` behavior, already pinned).
- The pointer replacement is a HAND, git-tracked edit — the machine never writes or deletes the file once the writer is gone.
- ADR rule: accepted ADRs change only by a superseding ADR — the retirement lands as a new entry, old consequence lines are not rewritten.
- Deferred-ledger preservation rule: no open finding leaves the file without a board home; orphan promotion precedes the pointer replacement.
- `dotnet build` + full serial `dotnet test` green before the on-disk migration; the migration pass must run on the NEW build (dev Host restart), not the old binary.
</constraints>

<acceptance-criteria>
- [ ] A materialization pass (strategy on, fresh or existing project) creates no `BACKLOG.md` and no tracked row for it; `planning_materialize_status` shows no index among written/skipped/tracked files.
- [ ] An existing tracked index row migrates on the next pass: repo-root file keeps hand content byte-for-byte with the managed block stripped, row deleted; archive-root copy deleted; drifted block refuses without force, force strips with a warning (033 intact) — all pinned by tests.
- [ ] `dotnet build` green; full serial `dotnet test` green across Core/Infrastructure/Api/Mcp suites.
- [ ] Living in-repo docs are consistent: README, CONTEXT, STRUCTURE, AGENTS, new ADR, crew kit x8 — no living doc instructs reading `BACKLOG.md` as an index; FEATURELIST and materialized trees untouched.
- [ ] Repo-root `BACKLOG.md` is the static pointer file; every open Deferred finding has a verified board home (orphans promoted before replacement).
- [ ] Final grep sweep: `BACKLOG\.md` residues exist only where intended (materializer legacy-cleanup code + its tests, AgentFileConflictScanner + fixture, kit tombstone header, FEATURELIST history, materialized trees).
</acceptance-criteria>

<refs>
- FEATURELIST/07-field-evidence-strengths.md — the silent-failure field case: index and task files diverged; `[?]` items buried where nobody scrolls.
- FEATURELIST/02-overmind-vs-files.md + FEATURELIST/01-token-cost-profile.md — hybrid rule "Overmind for state, files for contracts"; the index is state, 20K tokens of it.
- 033-materializer-clobber-guard — the kept-foreign/drift guard semantics this feature must preserve for the legacy row.
- 042-onboard-observability — onboard/materialization-status wording must describe the world without the index (quickstart already speaks only of the BACKLOG/ tree; confirm, no change expected).
- 075-materialization-git-hygiene — sibling territory: untracked BACKLOG/ by default; index removal reduces the tracked surface further.
- 125-microbatch-cleanup — the successor home for Deferred-style finding ledgers.
- 116-crewkit-in-repo — kit sources in-repo are repo-owned; repo -> home sync is the deploy path for the x8 edits.
</refs>

<obstacles>
- HUMAN FLAG — file fate: the spec PROPOSES static pointer (discoverability for MCP-less humans/tools; git history keeps everything); outright deletion is the alternative. Both satisfy the goal; the human picks at the consolidated validation sitting.
- HUMAN FLAG — hand content disposition: the repo-root file carries ~89 lines of hand-written content (legacy Order section + Deferred ledger). Replacement is lossy for that content by design; the orphan sweep (scope 4) is the safety net. Known candidates needing board homes: macOS `.pkg` installer half (blocked, no board feature), P0 `overmind.localhost` browser-proxy manual check (referenced by an ADR as "see BACKLOG.md"), zero-byte `overmind.db` boot crash hardening — each must be verified against the board/entries and promoted if orphaned. If the human prefers keeping the ledger in place, the pointer goes ABOVE the retained content instead of replacing it.
- HUMAN FLAG — out-of-repo surface: `BACKLOG.md` is referenced by the USER'S GLOBAL CLAUDE.md and other deployed kit copies. Those are outside this repo's repo-owned surface. This feature edits only in-repo kit sources (x8 files); applying them to `~/.claude` (and `.qwen`/`.gemini`/`.kimi-code` homes) via the repo -> home kit sync, plus any other project's own docs, is the human's separate act at result validation.
- ADR immutability vs dangling pointers: old ADR consequence lines (TECHNICAL-SOLUTIONS.md lines ~24/92/101/108) keep pointing at `BACKLOG.md` as a tracker. Resolved by the new superseding ADR noting the retirement; old text untouched. If the human prefers dated amendments in place, that is a validation-time override.
- Note (no action): `AgentFileConflictScanner` keeps flagging agent files that mention `BACKLOG.md` as a work source — post-retirement this becomes MORE useful (it flags stale instructions); its fixture tests stay.
</obstacles>

<log>
- 2026-10-06 [agent] — spec drafted from feature body + code research (writer located in Infrastructure/PlanningMaterializer, migration rides existing stale-row strip, hand-content ledger discovered at repo root)
- 2026-10-06 [agent] — validated with human proof: notification 54d89f79-6f81-41eb-a538-e1735aa8eef4 replied 2026-10-06 13:05:12 UTC
</log>
