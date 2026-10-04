---
feature: 073
slug: project-manifest
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 073 — Project manifest: AGENTS.md-аналог внутри Overmind — MCP-чтение для любого агента, UI-редактор на project-config, импорт agents/claude/qwen.md

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03.

## Task chain

1. **01 — Backend.** Project.Manifest field + migration AddProjectManifest; update-door contract (field on update request/response + validation); MCP read_project_manifest (projectId | cwd-resolved; absent → honest «not set», not an error); import-manifest action (AGENTS.md → CLAUDE.md → QWEN.md, first found, header line records the source, PathGuard containment); tests: round-trip, precedence, containment 400, honest-absent.
2. **02 — Web.** Config page: markdown textarea editor + save through the existing update mutation; «Import from agent file» button (shows which file was found); hint line — ties into the 071 scan when both land; tests on editor + import button.
3. **03 — Closeout.** Consistency (tool description vs contract); architect light; result with post-release click-check (read the manifest via MCP from an agent session, edit via UI).

## Verification

- Contract/unit tests (01), web tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear; scan hint soft-ties into 071.
- 2026-10-04 [agent] — validated with human proof: notification 542d6f4f-d436-479d-870e-684837274d47 replied 2026-10-04 12:00:45 UTC
</log>