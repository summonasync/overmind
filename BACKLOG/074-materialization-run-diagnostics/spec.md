---
feature: 074
slug: materialization-run-diagnostics
status: validated
created: 2026-09-25
validated: 2026-09-25
---

# Spec 074 — Materialization run diagnostics: lastSuccess/lastAttempt, per-file outcome, atomicity, diagnostic tool

<goal>
The materialized mirror stops being a black box: tool responses distinguish the last successful run from the last attempted one, report per-file outcomes structurally, run atomically (everything or nothing), and a new index_status-style MCP tool answers "what lives where, why did it refuse, when was the last success" without filesystem access.
</goal>

<context>
BobHouse 020 field note B1 (entry c7f34f40): a session saw "lastRun=never(failed … Refusing to overwrite…)" while the file had been regenerated a minute earlier — 054's state (LastMaterializedAt success-stamp + LastMaterializationError/FailedAt + SkipCount/SkipHead on Project, PlanningMaterializationReport from SyncAsync) reports only the tail: last failure prose + skip counts, conflating attempt with success and hiding per-file detail. Code verified 2026-09-25: PlanningMaterializer.SyncAsync returns PlanningMaterializationReport (written/skipped already exist in-memory); Project carries the 054 fields (:19-23); 067 tracks materialized files in tracking rows. No open user decisions: "write everything or nothing" is the body's literal contract, the diagnostic tool mirrors index_status's shape, and clobber-guard/naming policies are non-goals.
</context>

<decisions>
- Attempt vs success split: Project gains `LastMaterializationAttemptAt` (stamped by EVERY run start, success or fail); existing `LastMaterializedAt` is now explicitly lastSuccess semantics (stamped only on a fully successful run). Onboard and planning_materialize responses render both: "lastSuccess: … / lastAttempt: …" replacing the single lastRun prose line; the failed-attempt reason stays available (existing error fields) but no longer masks a later success.
- Atomicity = plan-then-write: SyncAsync first computes the FULL operation plan for the project (every file: write / skip(reason) / refuse(reason)) BEFORE touching disk. If any operation refuses, the run aborts entirely — zero files written that run — and the report records the refusals (atomic=false). Skips remain non-fatal by definition (skip = deliberate no-op, not a failure). Clobber-guard behavior (033/067) unchanged — it feeds the refuse channel.
- Persistent run report: Project gains a `LastMaterializationRun` TEXT column (additive migration) holding the JSON of the last completed OR aborted run: {startedAt, finishedAt, atomic (bool), written:[paths], skipped:[{path, reason}], refused:[{path, reason}]}. No size cap games — local SQLite TEXT; the old SkipCount/SkipHead fields stay for compat (still stamped, read paths migrate to the structured report).
- stalePaths: computed against tracking (067 rows) — tracked paths that the last run neither wrote nor skipped deliberately (i.e. present in tracking, missing from the materialization target set, or the file on disk drifted). Surfaced in the diagnostic tool response; recomputed on demand, not persisted.
- Diagnostic tool: `planning_materialize_status(projectId)` — ReadOnly, Idempotent, agent door: renders lastSuccess/lastAttempt, the persisted last-run report (per-file), tracked-file count, stalePaths, and the current materialization strategy/targetDir — the index_status of the mirror. Human door deliberately skipped (agent-facing diagnostic; onboard/planning_materialize cover humans).
- Compact echo discipline (037) holds: the diagnostic tool returns structured state, never file bodies.
</decisions>

<scope>
- Core/Infrastructure: Project fields (attempt stamp + run-report JSON) + additive migration; PlanningMaterializer plan-then-write rework (report object extended with refused + atomic flag); persistence of the report per run; Infrastructure tests (atomic abort leaves tree untouched, skip non-fatal, attempt stamped on failed run, success stamps both, report round-trip).
- Host: onboard + materialize responses carry the split fields (042/054 render sites); Api.Tests for both.
- MCP: planning_materialize_status tool (ReadOnly+Idempotent, compact structured response), Mcp.Tests wiring + a canned fixture.
</scope>

<anti-scope>
- Clobber-guard policy changes (033/067 semantics feed refusals, unchanged); git hygiene (075); task-file naming (076); web UI; human-door registration of the diagnostic tool; reindex/search.
</anti-scope>

<constraints>
- Additive migration; no destructive ops; existing fields stay populated (compat).
- No new NuGet packages; no comments; English strings.
- dotnet build 0 warnings; dotnet test green (baseline 1422).
</constraints>

<acceptance-criteria>
- [ ] Migration adds LastMaterializationAttemptAt + LastMaterializationRun cleanly on an existing dev database; 054 fields remain populated.
- [ ] Atomicity: a run where any file refuses writes NOTHING (tree byte-identical pre/post) and reports atomic=false with the refusal list; skip-only runs complete and stamp success.
- [ ] Attempt vs success: failed run stamps AttemptAt only; subsequent successful run stamps both; onboard/materialize render the split honestly.
- [ ] planning_materialize_status (ReadOnly+Idempotent) returns lastSuccess/lastAttempt, per-file last-run report, tracked count, stalePaths — without filesystem access.
- [ ] dotnet build 0 warnings; dotnet test Overmind.sln green (1422+).
</acceptance-criteria>

<refs>
Feature body 074 (BobHouse 020 B1 + "честное ограничение", entry c7f34f40); 054 (error persistence fields, RecordSyncFailureAsync); 033/067 (clobber guard + tracking — refuse channel and stalePaths source); 042 (onboard render sites); 037 (echo discipline); index_status (squeezr) as the UX template; microbatch #49, #56c (relatives).
</refs>

<log>
- 2026-09-25 — [need user attention on review] Decision supersede: 067-D2 "skip-not-abort" for clobber conflicts is replaced by refusal+atomic-abort — required by 074's validated "write everything or nothing"; a mixed tree was the exact BobHouse complaint. Old 067 tests re-pointed to refusal semantics. If the user prefers the old skip semantics, the atomicity contract needs rework — flag at the 074 done gate.
</log>
