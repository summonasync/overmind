---
feature: 073
slug: project-manifest
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 073 — Project manifest: AGENTS.md-аналог внутри Overmind — MCP-чтение для любого агента, UI-редактор на project-config, импорт agents/claude/qwen.md

> Grooming 2026-10-04 (user in session). User idea 2026-09-24.

## Goal

Every project carries a full markdown manifest in Overmind — what it is, stack, build/test commands, conventions — edited by the human on the project config page, read by ANY agent over MCP in one call. Overmind becomes the discovery point: find project → read manifest → know what you're dealing with. Today `list_projects` exposes one description line; agents must walk into DiskPath and parse repo files themselves.

## Decisions

- **`Project.Manifest`** (nullable markdown) — per the existing per-project field pattern; migration AddProjectManifest.
- **MCP `read_project_manifest`** (separate tool, projectId or cwd-resolved): 041 discipline — the listing stays light (one-line description), the manifest is the deep read. Absent manifest → honest «not set» response, not an error.
- **Human door:** manifest rides the existing project update door (field on update request/response); UI editor on the project config page — plain markdown textarea + save through the existing update mutation.
- **Import (one-way v1):** an import-manifest action reads the project's `{diskPath}/AGENTS.md` → `CLAUDE.md` → `QWEN.md` (first found wins), stores the body with a header line `<!-- imported from AGENTS.md -->`; UI button on the config page + the 071 scan lists a found manifest file as a hint line. PathGuard containment as everywhere.
- Description (one-liner) and Manifest stay distinct fields: chip vs deep read.

## Non-goals

- Writing the manifest back to repo files (AGENTS.md stays the human's file); multi-file manifests; cross-project manifest search.

<log>
- 2026-10-04 [agent] — groomed with the user in session: field+tool+editor+one-way import with precedence, honest-absent read, scan hint ties into 071.
- 2026-10-04 [agent] — validated with human proof: notification 542d6f4f-d436-479d-870e-684837274d47 replied 2026-10-04 12:00:45 UTC
</log>