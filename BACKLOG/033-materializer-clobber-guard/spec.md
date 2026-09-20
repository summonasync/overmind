---
feature: 033
slug: materializer-clobber-guard
status: draft
created: 2026-09-20
---

# Spec 033 — Materializer clobber guard: не затирать молча локальные правки в managed-файлах

<decisions>
- Refuse-by-default with force override — the exact mirror of the existing GuardOverwrites semantics for untracked files, now applied to tracked files whose disk content drifted. USER-APPROVED 2026-09-20 (gate question warn-vs-refuse → refuse). Rationale: the feature exists because silent loss already happened (Mindlash workflow #8, 2026-09-19: sub-agent appended to <obstacles> on disk; next force:false materialize regenerated the file from the board and the edits vanished without a word).
- The guard fires on the precise loss condition, not on any drift: row exists (file is tracked) AND disk hash ≠ row.Sha256 (local edits since our last write) AND plan content ≠ disk content (the write would actually change the bytes). Consequences: a local edit later absorbed into the board (plan == disk) writes byte-identical content — allowed, and Refresh heals the stale row (the transcribe path self-resolves); a locally deleted file (DriftKind.Missing) is recovery, never blocked.
- BACKLOG.md compares the managed block only (ManagedBlock.TryExtract + ContentHash.Sha256 — the semantics already used by DetectDriftAsync and RemoveTrackedCopyAsync). Hand-written sections outside the markers are legitimate by design and never trigger the guard.
- All drifted paths are collected first, then a single typed exception lists them — the orchestrator needs the full list in one shot, not a fail-fast loop of retries. New MaterializationFailureKind member LocalEditsBlocked appended at the END of the enum (enum-append rule, 023/08 review INFO-5); MaterializationException.LocalEdits(paths) carries every path in the message plus a one-line resolution hint: transcribe to the board (planning_upsert_artifact / planning_append_log) or re-run with force.
- Forced path: each clobbered file lands in the report warnings as "Overwrote locally modified file: <path>" — the report already surfaces in the API response and CLI output, no new channel needed.
- PlanningServiceGuard.SyncMaterializationAsync (auto-sync after board writes) keeps its swallow-to-warning semantics untouched: board mutations never fail because of drift; the disk stays stale and the explicit materialize becomes the loud door. This asymmetry is deliberate — the board is the source of truth and must stay writable.
- SkillMaterializer write path (kit files share the same PlanningMaterializedFile rows and the same silent-clobber shape) gets the same guard with the same refuse/force semantics; its force door is the kit materialize route (exact plumbing pinned at task level — the route today has no force flag).
- No web work: drift is already visible via the existing drift query (Modified items), and the refusal reaches callers as a typed ProblemDetails on the explicit materialize call.
</decisions>

<open-questions>
- none (refuse vs warn resolved by user gate 2026-09-20: refuse + force)
</open-questions>

<requirements>
Backend (PlanningMaterializer):
- SyncAsync: after BuildPlans, before WritePlansAsync, evaluate the loss condition for every planned path that has a row; BACKLOG.md via managed-block hash, feature files (feature.md/spec.md/plan.md/tasks/*.md) via whole-file hash (SHA256, ordinal-insensitive compare — the DetectDriftAsync precedent).
- On any hit with force:false → collect all hits → throw MaterializationException.LocalEdits(paths). On force:true → proceed and append one warning per clobbered file.
- MaterializationException: LocalEditsBlocked member appended last; factory with full path list + transcription hint in the message.
- PlanningMaterializationApi error mapping: LocalEditsBlocked maps to the same status family as OverwriteBlocked (verify actual code — 409 expected; pin in task).
Backend (SkillMaterializer):
- Same guard on the kit-file write path; force plumbing on the kit materialize route (minimal addition; shape pinned at task level).
Tests (Infrastructure + Api):
- Tracked task file hand-edited, board unchanged → explicit materialize (API) refuses with typed error listing the path; force:true regenerates the file AND the report warnings name it.
- Board mutation while drift exists → succeeds; auto-sync logs a warning; disk left stale (PlanningServiceGuard semantics unchanged).
- Hand-edit absorbed into the board (artifact updated to match disk) → next materialize writes byte-identical content without refusal; row hash heals.
- BACKLOG.md edit outside the managed block → never blocked; edit inside the block → blocked.
- Locally deleted tracked file → recreated, never blocked.
- Existing untracked-file OverwriteBlocked behavior unchanged (regression pin).
- Skill-file drift → kit materialize refuses (or the pinned force-door equivalent).
Verification:
- dotnet build + full test suite green.
- Live dogfood on this repo: append a marker line to a materialized tasks/NN-*.md, call POST /api/projects/{id}/planning/materialize → 409 LocalEditsBlocked listing the file; repeat with force → file regenerated, warning in report; revert (git checkout of the materialized tree is the force-path cleanup).
</requirements>

<anti-scope>
- No auto-transcription of local edits into the board, no merge/three-way logic — the human/agent decides.
- No web UI changes.
- No changes to DetectDriftAsync semantics or the drift endpoint.
- No changes to PlanningServiceGuard swallow semantics (board writes stay immune to drift).
- Orphan cleanup (files of board-deleted features) out of scope.
</anti-scope>

<log>
- 2026-09-20 — User gate passed 2026-09-20: refuse-by-default + force chosen over warn-only. Open question closed, decision recorded in decisions.
</log>
