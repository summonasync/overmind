---
feature: 033
slug: materializer-clobber-guard
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 033 — Materializer clobber guard: не затирать молча локальные правки в managed-файлах

<summary>
Feature 033 closed both doors: the materializers (planning + skill) no longer silently clobber hand-edited tracked files. Before any write, a pre-transaction guard compares the tracked row hash against disk AND the incoming plan; on a real loss condition (disk drifted AND plan differs) the sync refuses with a typed LocalEditsBlocked error listing every path plus a transcription hint; force overrides with per-file warnings in the report. The Mindlash incident class (sub-agent appends to <obstacles> on disk, next materialize wipes it silently) is dead.
</summary>

<tasks>
01 — Planning clobber guard: MaterializationFailureKind.LocalEditsBlocked (appended last) + MaterializationException.LocalEdits(paths) factory; GuardLocalEditsAsync in PlanningMaterializer.SyncAsync (BACKLOG.md managed-block-only compare); 409 arm in PlanningMaterializationApi. Review PASS-WITH-NITS (audits/review-033-01.md).
02 — Skill materializer guard: same guard unconditional on the kit path (no strategy concept — every write lands in the user workspace); MaterializeAsync force parameter; KitsApi Activate ?force= query flag; 409 arm; MaterializationReport +Warnings (additive contract). WARN-1 from review (untracked guard ignored force) fixed in-wave — both force doors symmetric. Review PASS-WITH-WARNINGS (audits/review-033-02.md), 956/956.
</tasks>

<deviations>
Accepted and deliberate (both judge-approved at review):
- Planning-side guard scoped to strategy == On (Archive root keeps free overwrite — consistent with GuardOverwrites and DetectDriftAsync; pin test Archive_WritesUnderDataDirectoryAndRewritesFreely requires it).
- BACKLOG.md with REMOVED managed-block markers does not block: ManagedBlock.Merge is append-only there, no byte is lost — the guard keys on the loss condition, not on any drift.
- Skill-side guard is unconditional (no archive root exists in SkillMaterializer — code-verified at review).
- Auto-sync (PlanningServiceGuard) swallow semantics untouched: board writes never fail on drift; the explicit materialize is the loud door. Live-proven: hand-edit present → board mutation still succeeds.
</deviations>

<evidence>
- Tests: 943 → 956 (+13 across Infrastructure/Api). Full suite green, 0 warnings, format clean.
- Live dogfood (isolated host, scratch HOME, port 4799, killed by port PID; prod 4761 untouched): hand-edited materialized spec.md → POST materialize {"force":false} = HTTP 409, detail lists the path + "Transcribe the edits to the board (planning_upsert_artifact / planning_append_log) or re-run materialize with force", file INTACT after refusal; {"force":true} = HTTP 200, warnings ["Overwrote locally modified file: <path>"], file regenerated (local marker gone).
- Kit door: API-test level (Activate_WithUntrackedFile_Returns409ThenForceAdoptsAndWarns + LocalEdits roundtrip in KitApiTests; Materialize_* in MaterializationTests). Live kit assembly flow judged out of dogfood scope.
- Review artifacts: audits/review-033-01.md (PASS-WITH-NITS), audits/review-033-02.md (PASS-WITH-WARNINGS, WARN-1 fixed same wave).
</evidence>

<follow-ups>
Microbatch queue items 29-31 (+ INFO findings of review-033-02): O(plans×rows) lookup, per-file warning test extension, StubOvermindApi port race.
</follow-ups>

<log>
</log>