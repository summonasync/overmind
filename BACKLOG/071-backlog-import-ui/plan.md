---
feature: 071
slug: backlog-import-ui
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 071 — Backlog import через UI: кнопка на странице настроек проекта (scan-превью → импорт → отчёт), HTTP-обёртка BacklogImporter

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03.

## Task chain

1. **01 — Human-door API.** `POST /api/projects/{id}/planning/import/scan` (optional sub-path; ParseFeatureAsync per folder; per-folder error isolation; already-imported marker by slug) + `POST .../import` (existing ImportFeatureAsync report); PathGuard containment identical to the agent door; API tests (scan finds folders, error isolation, containment 400, import report shape).
2. **02 — Web UI on project settings.** Scan button + folder list (new / already imported / error states), expandable preview, per-folder Import with confirm on re-import, report lines; React Query per house idiom; web tests on the list states.
3. **03 — Closeout.** Consistency (agent door vs human door share the importer), architect light, result with post-release click-check (scan a real project, import one folder).

## Verification

- API tests (01), web tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear; both doors share the importer.
- 2026-10-04 [agent] — validated with human proof: notification 35bc0a33-190c-4146-ad46-829d44bdd835 replied 2026-10-04 11:48:55 UTC
</log>