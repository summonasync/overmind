---
feature: 067
slug: materialization-import-tracking
status: validated
created: 2026-09-24
validated: 2026-09-24
---

# Plan 067 — Materialization × import: импорт регистрирует файлы в трекинге, skip-not-abort, planning_materialize MCP-триггер (OM-10)

<approach>
Three tasks along the data path: registration (importer writes tracking rows), semantics (guard conflicts skip instead of aborting), surface (MCP tool + onboard skip list + the git-discipline note). The 033 amendment (D2) is the sensitive spot — its test suite migrates from abort-assertions to skip-assertions, and the reviewer holds the line that skip still means do-not-touch.
</approach>

<tasks>
- [ ] 01-import-registers-tracking — Infrastructure: BacklogImporter registers every file it reads (feature/spec/plan/result/tasks) into PlanningMaterializedFiles with sha256 at read time, when PlanningPolicy resolves the project to On (Off/Archive → no rows, per spec D1); the materializer's ClearAsync must not be tripped by import-time rows on an On-project (it clears on strategy change only — verify). Tests: import a DMT-shaped tree into an On-project → PlanningMaterializedFiles matches files read (paths+shas); Off-project import adds zero rows; a subsequent mutation-triggered pass WRITES clean features without guard refusals (the OM-10 acceptance core). Verify: build + tests green → tasks/01-import-registers-tracking.md
- [ ] 02-skip-not-abort — Infrastructure: the materializer's guard conflicts (untracked file, locally-edited tracked file) produce skipped[] {relativePath, reason} entries — clean features still write, lastRun stamps on partial success; all-skipped → 409 (hard signal the mirror did not move); force keeps today's overwrite-with-warnings meaning. The report (HTTP + the materializer's return) carries written/skipped counts and the skip details. 033's suite migrates to skip-semantics (its protection cases assert skip+reason+file-untouched, not abort). Tests: one guarded file + clean siblings → siblings write, guard file byte-untouched, skip listed; all-guarded → 409; force → overwrite + warning lines. Verify: build + tests green → tasks/02-skip-not-abort.md
- [ ] 03-materialize-tool-onboard-docs — Mcp: planning_materialize(projectId, force=false) wrapping the existing POST endpoint, compact echo (written/skipped counts, force flag), description states what a rerun does; Host: the onboard materialization block gains skippedCount + head of skipped paths with reasons (lastError stays for total failures); pins for both (032 pattern). Docs: CONTEXT.md gains the git-discipline paragraph (who commits the mirror, what a checked-out other branch means) per spec R6; result artifact. Verify: build + full tests green → tasks/03-materialize-tool-onboard-docs.md
</tasks>

<risks>
- **033 semantic drift**: skip must never become write — the reviewer checks every migrated test still asserts the guarded file's bytes unchanged.
- **Row explosion on re-imports**: repeated imports must not duplicate rows (upsert by RelativePath, sha refreshed) — pinned in task 01.
- **Partial-success stamping honesty**: lastRun stamps only when at least one file wrote OR the pass was genuinely clean; an all-skipped pass must NOT stamp (nothing moved).
</risks>

<verification>
- dotnet build + dotnet test green after each task (baseline 1200).
- The OM-10 field scenario end-to-end (import → mutate → folders appear) pinned in task 01; skip semantics pinned in 02; tool+onboard pins in 03.
- Reviews: light on all three; the 02 review explicitly audits the 033 migration.
</verification>