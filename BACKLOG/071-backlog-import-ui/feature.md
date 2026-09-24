---
feature: 071
slug: backlog-import-ui
status: draft
priority: p1
version: 1
created: 2026-09-24
---

# Feature 071 — Backlog import через UI: кнопка на странице настроек проекта (scan-превью → импорт → отчёт), HTTP-обёртка BacklogImporter

<goal>
A human can import a BACKLOG/ tree into a project from the web UI — an Import button on the project settings page (the tab 066 builds): pick or type a repository root / BACKLOG folder path, see the dry-run scan (features, artifacts, warnings) and the import report. The backend is a new HTTP endpoint wrapping the existing BacklogImporter pipeline (today CLI-only — OM-7's human half).
</goal>

<requirements>
- HTTP: POST scan + POST import endpoints wrapping BacklogImporter (dry-run first, then commit) with the report shape the CLI already produces.
- UI: project settings page (066 tab) — path input (folder-picker integration with 050 where available, text input fallback), Scan → preview → Import flow, report rendering (created/updated/skipped with reasons).
- Import respects 067's tracking registration (imported files become tracked under strategy On) — build on 067, don't fork.
- Re-import safety: the preview makes overwrites visible before commit.
</requirements>

<non-goals>
- Agent-side import (058 — MCP door); drag-and-drop uploads (path-based, files live on the same machine); archive imports.
</non-goals>

<refs>
- User request 2026-09-24 ("импорт бэклога через ui на странице конфига проекта — для этого у нас уже есть фича" → the 066 settings tab).
- OM-7 (round 3); BacklogImportCli/BacklogImportCommand (the CLI pipeline being wrapped); 016 (import hardening lineage); 067 (tracking); 050 (folder picker).
</refs>