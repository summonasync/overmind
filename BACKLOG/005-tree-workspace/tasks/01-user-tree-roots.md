---
feature: 005
task: 01
slug: user-tree-roots
status: done
review: light
created: 2026-09-08
---

# 005/01 — User-defined tree roots

<goal>
Tree roots become user-defined and persisted: multiple roots stored as a JSON array in AppSettings under `treeRoots`, added via the existing folder picker, removable with a confirmation — unless a registered project lives under the root, in which case removal is blocked.
</goal>

<context>
Feature 004 shipped drive-based roots: `GET /api/system/tree-roots` returns all ready drives. This task replaces that with user roots. AppSettings is a key-value table (`AppSettings` entity, `Key`/`Value`) already used by SettingsApi — check `src/Overmind.Host/Api/SettingsApi.cs` and how settings keys are read/written before adding new code. Settings UI page is NOT part of this task — roots are managed from the tree itself.
</context>

<scope>
Backend (`src/Overmind.Host/Api/SystemApi.cs`, contracts in `SystemContracts.cs`):
- Settings key constant `treeRoots`, value = JSON array of normalized absolute paths (reuse the `NormalizePath` helper from 004/01).
- `GET /api/system/tree-roots` → `{ roots: [{ name, path, containsProjects }] }` — now reads the setting (empty array → empty roots). `name` = directory name (or the path itself for drive roots like `E:\`). `containsProjects` = true when any registered project's normalized DiskPath equals the root or starts with root + separator.
- `POST /api/system/tree-roots` body `{ path }` → validates: rooted, exists, is directory (400/404 ProblemDetails otherwise); normalizes; dedupes (exact match OR new path already covered by an existing root OR existing roots covered by the new path — reject with 409 and a clear message); appends; returns the new roots list.
- `DELETE /api/system/tree-roots?path=<abs>` → 409 `RootContainsProjects` when `containsProjects` is true; otherwise removes; returns the new roots list. Unknown path → 404.
- Tests in `tests/Overmind.Api.Tests/SystemApiTests.cs` (extend): add/list/dedupe/nested-reject/delete/unknown-delete-404/delete-blocked-by-nested-project (create project with DiskPath under the root). Keep drive-roots tests updated — drives are no longer the default.

Frontend (`web/`):
- `shared/api/system.ts`: replace `getTreeRoots` shape; add `addTreeRoot(path)`, `removeTreeRoot(path)`.
- `features/projects/components/project-tree.tsx`: roots header row with an "Add root folder" button → uses the existing browse-folder picker (`browseFolder` in `shared/api`), then `addTreeRoot`. Each root node gets a remove (X) button: disabled with tooltip "Contains registered projects" when `containsProjects`; otherwise opens a small confirm (reuse `Modal` or a window.confirm-free inline confirm — match house modal style). Empty roots: centered hint + the same add button. Roots still expand via `list-directory` as today.
- React Query: mutations invalidate the tree roots key (`projectKeys.tree("roots")`).

</scope>

<anti-scope>
- No Settings-page UI for roots.
- No active-project navigation changes (task 02).
- No reorder/alias editing of roots.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green; `npm run lint && npx tsc --noEmit && npm run build` clean.
- No new packages. No comments unless non-obvious. English only.
- Vertical-slice EF-in-handler style is fine (ADR-B); watch the ~300-line trigger on SystemApi — split into `SystemRootsApi.cs` if crossed.
</constraints>

<acceptance-criteria>
- [ ] Roots persist across host restarts (AppSettings).
- [ ] Add validates + dedupes + rejects nested roots (409).
- [ ] Delete blocked (409 + disabled X with reason) when a project lives under the root; confirmation otherwise.
- [ ] Empty state shows the add affordance, no drives.
</acceptance-criteria>

<refs>
- @../feature.md
- BACKLOG/004-projects-tree-view/tasks/01-disk-enumeration-api.md — NormalizePath + list-directory
- src/Overmind.Host/Api/SettingsApi.cs — AppSettings read/write conventions
</refs>

<obstacles>
2026-09-08 — ApiFactory gained optional `databasePath`/`preserveDatabase` ctor params (test-infra only) so the restart-persistence test can point two host instances at the same SQLite file; default behavior unchanged. Delete-multi-root test needs a sibling temp dir — a second root nested under the first is correctly rejected as covered (409), so sandbox-internal dirs cannot serve as the "other" root.
</obstacles>

<log>
2026-09-08 — task created from user direction (multiple roots as array, removal confirmation + project guard).
2026-09-08 — implemented. Backend: roots moved out of drive-based SystemApi into new `SystemRootsApi.cs` (~250-line trigger avoided on SystemApi); `SettingKeys.TreeRoots = "treeRoots"` holds a JSON array of normalized paths via AppSettingsService; GET returns `{ name, path, containsProjects }`, POST validates rooted/exists/directory (400/404) + dedupes exact/nested/covered (409), DELETE 404s unknown and 409s `RootContainsProjects`. Frontend: add-root via browse-folder picker, per-root X disabled with tooltip when containsProjects, house-style Modal confirm otherwise, empty state with accent add button, mutations invalidate `projectKeys.tree("roots")`. Verification: dotnet build 0/0; dotnet test 189/189 (Core 30, Infra 48, Mcp 34, Api 77 — 12 new roots tests, drive test replaced); web lint 0 errors/6 known warnings, tsc clean, next build static export OK.
2026-09-08 — live curl smoke by orchestrator: empty roots → add → nested 409 → delete-guard 409 `RootContainsProjects` all confirmed. Found a UX deadlock: parent root could never be added once a narrower root existed (add-parent 409'd, remove-child 409'd by the project guard). Fix: POST now ABSORBS covered roots (merged list returned) instead of 409; test updated (`TreeRoots_AddParentOfExistingRoot_AbsorbsNestedRoots`). Full suite 189/189.
</log>
