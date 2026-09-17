---
feature: 004
slug: projects-tree-view
status: validated
created: 2026-09-08
validated: 2026-09-17
---

## Shape

Two sequential tasks: backend endpoints first (frontend codes against real contracts), then the sidebar tree + sync flow. No DB changes, no new packages.

## Task 01 — disk enumeration API (`tasks/01-disk-enumeration-api.md`)

`src/Overmind.Host/Api/SystemApi.cs` + contracts:
- `GET /api/system/tree-roots` → `{ roots: [{ name, path }] }` — ready drives on Windows (`DriveInfo.GetDrives()` where `IsReady`), `~` and `/` on macOS.
- `GET /api/system/list-directory?path=<abs>` → `{ path, truncated, entries: [{ name, path, accessible, projectId, projectName }] }` — directories only, single level, skip hidden/system, cap 500 (`truncated: true` past cap), unreadable dirs listed with `accessible: false`, project match via normalized path equality.
- Path normalization helper: `Path.GetFullPath` + trim trailing separators + `StringComparison.OrdinalIgnoreCase` on Windows — fixes the hardening nit about DiskPath comparison and is shared with project matching.
- Project lookup: load all projects' DiskPaths once per request, map normalized path → (id, name).
- Errors: missing/invalid `path` → 400; path doesn't exist or not a directory → 404; not rooted → 400.
- Tests (`tests/Overmind.Api.Tests/SystemApiTests.cs`): roots shape; listing a temp dir tree; hidden/system skipped; cap + truncated flag; project match by exact path, by path with trailing separator, case-insensitive on Windows; non-dir 404; inaccessible dir → `accessible: false` (simulate with a directory junction to a missing target or locked ACL — pick what's reliable on Windows CI-less box).

Verification: `dotnet build && dotnet test` green.

## Task 02 — sidebar tree + register flow (`tasks/02-sidebar-tree.md`)

`web/`:
- Move `browseFolder` from `features/system` to `shared/api` (closes review WARNING-4); update the project-form import.
- `features/projects/queries.ts`: `projectKeys.tree(path)` factory; hooks `useTreeRoots()`, `useDirectory(path, expanded)` (React Query, `enabled: expanded`, `staleTime` 60s).
- `features/projects/components/project-tree.tsx`: recursive lazy tree — node expand triggers its `useDirectory`; per-node loading/error inline; project nodes highlighted (`text-primary`, folder-open icon) + `<a href="/?project=<id>">`; inactive nodes muted with a "register" icon-button.
- Sidebar (`widgets/app-shell/ui/app-sidebar.tsx`): Projects nav item gets an expand chevron revealing the tree below it; expansion state in React state (session-only per R1).
- Register flow: register action opens the existing create-project modal with Disk Path prefilled; on success invalidate `projectKeys.lists()` + all tree queries (`projectKeys.tree` prefix) so the new node lights up (R5).
- Deep link: Projects page reads `?project=<id>` on load and auto-opens that project's detail.
- Projects flat list on the page stays as-is.

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean; manual check — expand tree to a real project folder, highlight + navigation work, register an unregistered folder from the tree.

## Risks

- Sidebar width 240px — deep paths need truncation with `title` tooltip; indent per depth, cap visual depth ~8 (deeper still works, just indented).
- Drives with thousands of dirs (e.g. `C:\Windows`) — cap 500 + `truncated` flag keeps it sane; no recursion protects us.
- React Query cache per path keeps re-expands instant; invalidation on register is prefix-wide by design.

<log>
- 2026-09-17 — re-imported from BACKLOG/004-projects-tree-view/plan.md
</log>
