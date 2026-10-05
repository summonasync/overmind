---
feature: 108
slug: sidebar-projects-style-and-auto-expand
status: draft
created: 2026-10-05
---

# Result 108 — Sidebar: Projects row/tree in the nav row style + auto-expand to the open project

> Drafted by web-coder. Task 01 scope shipped in full; orchestrator runs light review before closing.

## Shipped

- **Row parity.** The Projects row is a standard nav row: identical geometry and tone classes as Kits/Search/Settings (`flex items-center gap-2.5 rounded-md border-l-2 px-3 py-2 text-sm font-medium transition-colors`, `border-primary bg-primary/10 text-primary` active, `border-transparent text-muted hover:bg-surface-raised hover:text-foreground` idle). A test pins the exact-count parity (2 occurrences of each string in the file).
- **Integrated chevron.** The caret moved inside the row as a right-aligned affordance (`shrink-0` button after the `flex-1` label link), no longer a visually separate left-side button. `aria-expanded` and the Expand/Collapse `aria-label` are kept.
- **Tree rows.** Project leaf rows follow the nav rhythm — `border-l-2` + primary tone + `bg-primary/10` when selected, muted + nav hover otherwise; depth stays as indent (`paddingLeft: depth * 12`), not a border. `aria-current="page"` on the selected row.
- **Auto-expand.** While the URL carries `?project=`, both layers open regardless of saved state: the Projects section (sidebar) and the folder chain from the matching tree root down to the project — pure `projectAncestorPaths(rootPaths, diskPath)` over the project's `diskPath` from the cached project list. On non-project pages the saved session state applies again: auto-expansion never writes `saveExpandedPaths`/`saveProjectsSectionOpen`.
- **Manual toggle on top.** On a project route toggles are visit-local (`openOverride` for the section, `routeCollapsed` for tree folders); off a project route toggles persist to sessionStorage exactly as before.
- The old pathname gate (`/entries` || `/dashboard`) is gone: any page whose URL carries `?project=` highlights and expands (today every such page treats the param as a project id: /, /dashboard, /entries, /issues).

## Files

- `web/src/widgets/app-shell/ui/app-sidebar.tsx` — row unification, section auto-open, visit-local toggle
- `web/src/features/projects/components/project-tree.tsx` — expansion union (saved ∪ auto, minus route collapses), selection on any page, leaf row rhythm
- `web/src/features/projects/lib/tree-paths.ts` — new: pure `projectAncestorPaths`
- `web/tests/tree-paths.test.mts` — new: 9 tests on the ancestor-chain math
- `web/tests/sidebar-projects.test.mts` — new: 9 tests pinning row parity, integrated chevron, auto-open, saved-state interplay, selection highlight

## Gates (from web/, single chained run)

- `npm run lint` — 0 errors (6 pre-existing warnings in untouched files: img elements, window.location.assign)
- `npx tsc --noEmit` — clean
- `npm test` — 200 pass / 0 fail (18 new)
- `npm run build` — exit 0, static export prerendered

## Post-release click-check

1. Save a collapsed section in sessionStorage, open `/dashboard?project=<id>` → section opens, the folder chain to the project is expanded, the row shows the primary tone.
2. Navigate to `/kits` → section and tree return to the saved state (collapsed stays collapsed).
3. On a project page collapse the section via the chevron → stays closed for the visit; a non-project page restores the saved state.
4. On a project page collapse an auto-expanded folder → closed for the visit, saved state untouched.
5. Visual: chevron right-aligned inside the Projects row, row geometry matches Kits/Search/Settings; keyboard focus rings work on both the label link and the chevron.

<log>
- 2026-10-05 [agent] — web-coder: result drafted after green gates (lint 0 errors, tsc clean, tests 200/200, build ok); click-check steps listed for post-release verification.
</log>