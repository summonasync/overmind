---
feature: 067
slug: materialization-import-tracking
status: inProgress
priority: p0
version: 1
created: 2026-09-24
---

# Feature 067 — Materialization × import: импорт регистрирует файлы в трекинге, skip-not-abort, planning_materialize MCP-триггер (OM-10)

<goal>
Materialization works on projects whose BACKLOG/ was IMPORTED, not born on the board. Today the importer reads feature.md files but never registers their hashes in PlanningMaterializedFiles — the clobber guard then refuses to write ("exists on disk but is not tracked"), and because one refusal aborts the whole pass, NOTHING materializes (DMT: zero rows in tracking since 2026-09-22, zero new folders on disk despite board mutations). The guard built to protect foreign files is blocking Overmind's own imports.
</goal>

<requirements>
- The import path registers every file it reads into PlanningMaterializedFiles with its sha — imported files ARE tracked by definition.
- A per-file conflict (untracked/guarded) becomes skip + a report line — one conflict never aborts the pass; clean features still write.
- The onboard materialization block surfaces skipped files with reasons, not only the first error (extends 054's failure visibility).
- New MCP tool planning_materialize(projectId, force?) — manual re-run trigger; today the only retry path is an arbitrary board mutation.
- Design note from the field (non-blocking, document in spec): who commits the mirror and what happens when the checked-out branch differs from the feature's branch — needs a stated discipline, not silent dirt.
</requirements>

<non-goals>
- Weakening the clobber guard for genuinely foreign edits (033 semantics stay).
- Entries materialization (051).
- Git branch integration/automation.
</non-goals>

<refs>
- FEATURELIST/overmind-features-2026-09-24.md OM-10 (P0, acceptance scenario inside: after a mutation BACKLOG/mmt-36-2/ appears; onboard shows fresh lastRun; the 007 feature.md gets a tracking row or an explicit skip line).
- 033-materializer-clobber-guard (the guard being tripped); 054 (failure visibility + default on); PlanningMaterializedFiles tracking.
- Field verdict: "while materialization falls over, the mode 'bodies in files, state in DB' remains a promise" — top of the four steps to "збс".
</refs>

<log>
- 2026-09-24 [auto] — artifact created → inProgress
</log>
