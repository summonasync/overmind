---
feature: 071
slug: backlog-import-ui
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 071 — Backlog import через UI: кнопка на странице настроек проекта (scan-превью → импорт → отчёт), HTTP-обёртка BacklogImporter

> Grooming 2026-10-04 (user in session). The import machinery is complete (BacklogImporter, agent door `planning_import_feature`, ParseFeatureAsync as a ready scan primitive); the human door is missing — imports today happen only through MCP.

## Goal

A button on the project settings page: scan the project's `BACKLOG/` tree → preview what would import → import per feature folder → report. The human should not need an MCP session to bring a materialized backlog home.

## Decisions

- **Two human-door endpoints** (project-scoped, same PathGuard containment as the agent door): `POST /api/projects/{id}/planning/import/scan` (body: optional sub-path under the project root) → list of found feature folders, each with slug/title/status/artifact counts + already-imported marker (compare by slug) — built on `ParseFeatureAsync`, no writes; `POST .../import` (body: folder name) → the existing `ImportFeatureAsync` report. Scan failure per folder does not kill the scan — the folder is listed with an error note.
- **UI on project settings:** a section listing scanned folders with per-folder state (new / already imported / error), expandable preview, per-folder Import button + report line; Import on an already-imported folder allowed (re-import is the established update path) with the confirm step naming it.
- Scan is read-only and bounded (one level of feature folders; no recursion into artifacts).

## Non-goals

- Bulk import-all (per-folder keeps the human in control v1); editing imported content in the UI; import from outside the project root (containment stays).

<log>
- 2026-10-04 [agent] — groomed with the user in session: scan on ParseFeatureAsync (no writes), per-folder import, UI on project settings; bulk-import deferred.
- 2026-10-04 [agent] — validated with human proof: notification 35bc0a33-190c-4146-ad46-829d44bdd835 replied 2026-10-04 11:48:55 UTC
- 2026-10-04 [agent] — [review caveat from 071/01, spec body untouched — kept frozen] «No writes» in this spec means the BOARD: ParseFeatureAsync carries one DB side effect — unknown frontmatter keys get registered (FrontmatterKeys) on first sight, identical to the agent import door's behavior; declared in task 01's log, recorded as review WARNING 1 (audits/review-071-01.md), rides the 071/03 result.
</log>