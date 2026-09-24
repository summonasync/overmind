---
feature: 067
slug: materialization-import-tracking
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Spec 067 — Materialization × import: импорт регистрирует файлы в трекинге, skip-not-abort, planning_materialize MCP-триггер (OM-10)

<decisions>
- **D1 — Imported files are tracked: the import path registers every file it reads (feature/spec/plan/result/tasks) into PlanningMaterializedFiles with its sha256 at read time — when the resolved strategy is On (agent).** "Tracked by Overmind" is true by definition for files the importer consumed; the clobber guard was built to protect FOREIGN edits, not to veto our own reads. Strategy scoping: under Off the materializer does not own the tree (ClearAsync semantics) — no rows; under Archive the writer targets the archive root, so repo files are not its output — no rows either. Only On has path parity between import and materialization.
- **D2 — A per-file conflict becomes skip-plus-report; one conflict never aborts the pass (agent, deliberate 033 amendment).** Today the first untracked/edited file throws and NOTHING writes (DMT: zero new folders despite board mutations). New contract: the pass writes every clean feature, collects skipped[] {relativePath, reason} and warnings, persists lastRun on the partial success, and the report carries written/skipped counts. The 409 abort survives only as the edge where the pass could write NOTHING (everything skipped) — the caller still needs a hard signal that the mirror did not move. 033's core (never silently overwrite) is untouched: skip means do-not-touch, with a stated reason. 033's test suite moves from abort-semantics to skip-semantics — called out as an intentional amendment with this spec as the provenance.
- **D3 — planning_materialize MCP tool (projectId, force = false) wraps the existing HTTP endpoint (agent).** PlanningMaterializationApi already serves POST /projects/{id}/planning/materialize {force} with a full report — the tool is a thin adapter wrapper (037-echo style: the report is compact by construction). Today the only re-run path is an arbitrary board mutation — the field called that out explicitly.
- **D4 — Onboard shows the skip list, not just the first error (agent).** The materialization block gains skippedCount and the first few skipped paths with reasons (extends 054's failure visibility; lastError remains for total failures).
</decisions>

<open-questions>
- Skip persistence: keep skipped[] only on the last report (in-memory per pass + persisted summary fields on Project) vs a full row per skip? Recommendation: summary fields (lastSkippedCount, lastSkippedHead) — the report endpoint can re-derive detail from a fresh run; a skip table is granularity nobody asked for.
- Force semantics under the new model: force = overwrite-skipped-files (today's meaning) or force = rerun? Recommendation: keep today's meaning (force writes over guarded files with per-file warnings) — the rerun need is served by the new tool.
</open-questions>

<requirements>
- R1 — Import registers rows (D1): per-file sha at read time, only under strategy On; covered by a test importing a folder tree into an On-project and asserting PlanningMaterializedFiles matches the files read.
- R2 — Skip-not-abort (D2): guard conflicts produce skipped entries; clean features still write; lastRun stamps on partial success; all-skipped → 409 as today; 033's suite updated to the new semantics (its clobber protection cases assert skip+reason, not abort).
- R3 — planning_materialize tool (D3): adapter wrapper, compact echo (written/skipped counts + force flag), description states what a rerun does.
- R4 — Onboard block (D4): skippedCount + head of skipped paths with reasons; pinned.
- R5 — The field acceptance scenario passes: import DMT-shaped tree → mutate board → BACKLOG/mmt-36-2/ appears on disk; onboard shows fresh lastRun; the 007 feature.md either has a row or an explicit skip line.
- R6 — Git-discipline design note (non-blocking, from the field): a short paragraph in CONTEXT.md stating who commits the mirror and what a checked-out-other-branch means for the mirror — documentation, not automation.
</requirements>

<non-goals>
- Weakening 033 for genuinely foreign edits (skip = do-not-touch stays).
- Entries materialization (051); git branch automation; importer round-trips (058).
</non-goals>

<context>
Field source: FEATURELIST/overmind-features-2026-09-24.md OM-10 (P0) — DMT: zero rows in both tracking tables since 2026-09-22, one refusal aborts the pass, no manual trigger; "while materialization falls over, the mode remains a promise". Code survey: BacklogImporter.TryReadAsync consumes files with no sha writes; PlanningMaterializer guards at :51/:61 consult PlanningMaterializedFiles; the HTTP materialize endpoint exists with force; no MCP tool. Our own project never hit this because its features were board-born, not imported.
</context>