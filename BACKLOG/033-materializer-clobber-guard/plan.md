---
feature: 033
slug: materializer-clobber-guard
status: draft
created: 2026-09-20
---

# Plan 033 — Materializer clobber guard: не затирать молча локальные правки в managed-файлах

<approach>
Two backend tasks, no web work. The guard is a pure pre-write step in each materializer's sync path, reusing the existing row-hash infrastructure (PlanningMaterializedFile.Sha256) and the existing refuse/force precedent (GuardOverwrites + MaterializePlanningRequest.Force). Pinned during spec walkthrough:
- OverwriteBlocked maps to HttpErrors.Conflict (409) in BOTH private copies of MaterializationError (PlanningMaterializationApi.cs:68 and KitsApi.cs:293) — LocalEditsBlocked needs an explicit arm in each BEFORE the 500 fallback.
- Kit materialize route (KitsApi activate+materialize) passes no force today — the skill-side force door is a new optional flag on that route.
- Auto-sync (PlanningServiceGuard.SyncMaterializationAsync) swallows to warning — untouched, board writes stay immune to drift.
Dogfood runs on an isolated host with the new build (scratch OVERMIND home, bound to a free loopback port, kill by port PID only — the standing incident lesson); prod deploy rides the next pack cycle, not this feature's gate.
</approach>

<tasks>
01 — Planning clobber guard (core): MaterializationFailureKind.LocalEditsBlocked (appended last) + MaterializationException.LocalEdits(paths) factory with transcription hint; PlanningMaterializer.SyncAsync pre-write guard (loss condition row+drift+real-byte-change; BACKLOG.md managed-block-only compare; collect-all-then-throw; force → per-file warning "Overwrote locally modified file"); LocalEditsBlocked → 409 arm in PlanningMaterializationApi.MaterializationError; test matrix per spec (refuse/force, board-write-immune, transcribe-heal, BACKLOG.md outside/inside managed block, missing-file recovery, untracked OverwriteBlocked regression pin).
02 — Skill materializer guard + force door: same guard on SkillMaterializer write path (kit files, whole-file hash); MaterializeAsync gains force parameter (default false); kit materialize route accepts the force flag; LocalEditsBlocked → 409 arm in KitsApi.MaterializationError; skill-drift refuse/force tests. Then closeout: build + full suite, isolated-host dogfood of BOTH doors (planning 409 + force; kit refuse), result artifact.
</tasks>

<verification>
- dotnet build + dotnet test green (both tasks).
- Isolated host dogfood: scratch project, materialize, hand-edit a task file, POST materialize → 409 LocalEditsBlocked naming the file; force → regenerated + warning in report; git-revert cleanup. Kit path: same shape via kit route.
- Regression: existing materializer + importer suites untouched-green.
</verification>

<risks>
- GuardOverwrites ownership logic (row + other-root absence) is subtle: the new guard must NOT reuse the "owned" concept — it keys purely on row-exists + hash mismatch + plan≠disk. Task 01 must keep the two guards textually adjacent but logically separate.
- FilePlan for BACKLOG.md carries the managed-block hash already (BuildBacklogPlan sets Sha256 = block hash) — the disk-side compare must hash the extracted block, not the whole file; same asymmetry as DetectDriftAsync.
</risks>