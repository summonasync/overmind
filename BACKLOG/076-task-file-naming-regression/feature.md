---
feature: 076
slug: task-file-naming-regression
status: done
priority: p1
version: 1
created: 2026-09-24
---

# Feature 076 — Bug: task files materialize as 01-task-01.md — title slug dropped from filenames

<goal>
Task files in the materialized mirror are navigable again: file names carry the artifact title slug (01-cpm-обновление-пакетов-aspire-13-5.md), not the degraded 01-task-01.md form observed on fresh ceremony output.
</goal>

<scope>
Regression: newly materialized task files come out as 01-task-01.md … 06-task-06.md — the artifact title no longer travels into the filename, the mirror is unnavigable for humans (must open every file). BacklogNaming.cs owns the naming path; find what dropped the title (likely the 067-series import-tracking / skip-not-abort work) and restore it.
</scope>

<acceptance>
- Fresh ceremony output produces titled task filenames matching the old scheme
- Cyrillic titles slugify the same way the old files did (readable, not octal)
- Regression test pinning the naming scheme
</acceptance>

<refs>
Source: note «Полевые заметки агента (BobHouse 020, 2026-09-24)», B3 (entry c7f34f40-3cea-49ec-8e23-bb1d8fe8e9c9). Suspect: 067-series changes around PlanningMaterializer/BacklogNaming.</refs>

<log>
- 2026-09-24 [auto] — artifact created → inProgress
- 2026-09-24 [agent] — by user request in-session — task 9091777f done (4/4 AC), result cbb8c10a recorded, full suite 1331/1331
</log>
