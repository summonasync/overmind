---
feature: 073
slug: project-manifest
status: inProgress
priority: p1
version: 1
created: 2026-09-24
---

# Feature 073 — Project manifest: AGENTS.md-аналог внутри Overmind — MCP-чтение для любого агента, UI-редактор на project-config, импорт agents/claude/qwen.md

<goal>
Every project carries a full markdown manifest in Overmind — what the project is, stack, build/test commands, conventions — editable by the human on the project config page and readable by ANY agent over MCP in one call. Today list_projects exposes only a one-line description; an agent discovering projects on a machine must walk into DiskPath and parse AGENTS.md/CLAUDE.md itself. The manifest makes Overmind the discovery point: find the project → read its manifest → know what you're dealing with.
</goal>

<requirements>
- Project.Manifest (markdown body, nullable) — storage per the existing per-project field pattern.
- MCP: a read path for the manifest (read_project_manifest or equivalent — 041 metadata/listing discipline: the project listing stays light, the manifest is the deep read).
- UI: manifest editor on the project config page (the tab 066 builds / 071 populates) — plain markdown textarea, save via the existing project update door.
- Import: the backlog importer (071 flow / CLI path) picks up the project's agent-instruction file on import — precedence AGENTS.md → CLAUDE.md → QWEN.md (first found wins, the source recorded in the manifest header line); one-way into Overmind in v1.
- The one-line description and the manifest are distinct fields (listing chip vs deep read).
</requirements>

<non-goals>
- Writing the manifest back to repo files (AGENTS.md stays the human's file; v1 is one-way import).
- Multi-file manifests (one markdown body).
- Cross-project search over manifests (053 territory for artifacts; manifests ride list/search later if asked).
</non-goals>

<refs>
- User idea 2026-09-24 ("описание проекта в overmind — аналог agents.md, менять на странице проект-конфига, при миграции импортировать agents/claude/qwen.md").
- list_projects (the discovery surface today); 041 (metadata-vs-deep-read discipline); 066/071 (the config page host); BacklogImporter (the import hook); 052-briefing (start-of-session sibling — briefing is per-session state, the manifest is per-project identity).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
</log>
