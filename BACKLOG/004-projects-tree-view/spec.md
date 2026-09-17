---
feature: 004
slug: projects-tree-view
status: validated
created: 2026-09-08
validated: 2026-09-17
---

## Goal

Sidebar Projects becomes a live window into the disk: a lazy folder tree where registered projects pop out as highlighted, clickable nodes, and everything else is a muted inactive node that can be registered in place. The user sees the real folder structure of the PC and where their Overmind projects sit inside it.

## Decisions

- **Enumeration endpoint**: `GET /api/system/list-directory?path=<abs>` returns directories only: `{ path, entries: [{ name, path, projectId, projectName }] }`. `projectId/projectName` are set when the normalized directory path equals a registered project's DiskPath — matching reuses the trailing-separator-insensitive comparison (see hardening note about `Path.GetFullPath`; fix it as part of this task).
- **Roots endpoint**: `GET /api/system/tree-roots` → ready drive roots on Windows (`C:\`, `D:\`, …), `~` and `/` on macOS.
- **Lazy only**: the tree enumerates a directory when its node expands. No recursive scans anywhere — the endpoint is always single-level.
- **Caps**: max 500 directories per response (remainder reported as `truncated: true`); hidden and system directories are skipped; unreadable directories return the entry with `accessible: false` instead of failing the whole listing.
- **Sync flow**: inactive node → inline action "Register as project" → existing create-project modal opens with Disk Path prefilled. No new backend — reuse `POST /api/projects`.
- **Navigation**: clicking a project node goes to the Projects page with that project opened (deep link `/` + selected project state, MPA-compatible: `/?project=<id>`).
- **Placement**: tree UI lives in `features/projects` (tree components + queries); backend goes into the existing `SystemApi` next to browse-folder. `browseFolder` cross-feature import moves to `shared/api` in the same pass (closes review WARNING-4).
- **Security**: the endpoint exposes the local folder structure to whatever can reach the loopback API — accepted for the single-user MVP, same trust level as browse-folder. Documented in CONTEXT.md.
- **No DB changes.**

## Requirements

- R1. Sidebar Projects item expands into the tree; expansion state persists for the session (React state, no localStorage).
- R2. Project nodes are visually highlighted (primary color + folder-open icon) and clickable → `/?project=<id>`.
- R3. Inactive nodes are muted, expandable, carry the "Register as project" action.
- R4. Each expand issues one `list-directory` call; loading/error states are per-node; errors (e.g. access denied) render inline and don't break siblings.
- R5. Register-as-project reuses the existing project form + folder picker; after creation the new project node lights up in the tree without a manual refresh (query invalidation).
- R6. `dotnet build && dotnet test` green; `npm run lint && npx tsc --noEmit && npm run build` clean.

## Non-goals

- Files, file watching, bulk operations, drag-and-drop, disk mutations (rename/delete) from the tree.
- Caching directory listings server-side.

## Open questions for the user

1. Projects page itself: keep the current flat list as the main content (tree only in sidebar), or replace the page with the tree? **Draft assumes: keep the list, tree lives in the sidebar.**
2. `/?project=<id>` on the Projects page: open the project detail modal automatically? **Draft assumes: yes.**

<log>
- 2026-09-17 — re-imported from BACKLOG/004-projects-tree-view/spec.md
</log>
