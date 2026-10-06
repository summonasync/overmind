---
feature: 005
task: 02
slug: tree-workspace
status: done
review: light
created: 2026-09-08
---

# 005/02 — Active project context in the tree

<goal>
Clicking a project in the tree makes it the session's active project: navigation goes to `/entries?project=<id>`, the node is highlighted as active, the tree keeps its expansion state across MPA reloads, and the Entries nav item deep-links to the last active project.
</goal>

<context>
MPA navigation: every click is a full page load (Next 16 static-export router broken upstream — plain `<a href>` only). React state dies per page, so tree expansion and the Projects-section open state must persist in sessionStorage (tab-scoped, no localStorage). The Entries page already supports `?project=<id>` fully (title, filters, CRUD) and shows a "Select a project first" prompt without it. Built on task 01's tree (roots in AppSettings).
</context>

<scope>
`web/`:
- `features/projects/lib/tree-session.ts`: tiny sessionStorage helper — get/set expanded paths (string[]), projects-section open (bool), last active project id. Guarded for SSR (no window during prerender).
- `project-tree.tsx` / `TreeNode`: expanded state syncs with the session helper (init lazily on mount via rAF pattern like the random Kerrigan, or read in useState initializer guarded for SSR — pick the hydration-safe option). Project nodes link to `/entries?project=<id>` instead of `/?project=<id>`.
- Active highlight: the tree reads the current URL (`usePathname` + `useSearchParams`) — when pathname is `/entries` and `?project=<id>` matches a project node, that node gets the active treatment (filled background `bg-primary/10`, primary text, maybe a left border — match sidebar active-nav style). Highlight is derived from the URL, not stored.
- `app-sidebar.tsx`: Projects-section open state persists via the session helper. The Entries nav item href becomes `/entries?project=<lastActive>` when a last active project exists (read on mount, hydration-safe), plain `/entries` otherwise. Clicking a project node also records it as last active.
- Register flow stays as-is (task 01).
</scope>

<anti-scope>
- No backend changes.
- No changes to the Entries page itself.
- No localStorage, no cross-tab sync.
</anti-scope>

<constraints>
- FSD placement as in 004; no cross-feature imports.
- Hydration-safe sessionStorage access (static export prerenders without window) — no synchronous setState in effects (react-hooks lint rule); use the rAF pattern.
- `npm run lint && npx tsc --noEmit && npm run build` clean.
</constraints>

<acceptance-criteria>
- [ ] Project node click → `/entries?project=<id>`; node shows active style on that page.
- [ ] Expanding nodes, navigating away and back keeps the tree expanded (sessionStorage).
- [ ] Entries nav item deep-links to last active project after one was clicked; plain `/entries` in a fresh tab.
- [ ] Projects nav label still opens the full list at `/`.

<refs>
- @../feature.md
- tasks/01-user-tree-roots.md
- BACKLOG/004-projects-tree-view/tasks/02-sidebar-tree.md — tree structure this builds on
</refs>

<obstacles>
- Deviated from the literal `useSearchParams` hint in <scope>: it forces client rendering of the subtree up to the nearest Suspense boundary during static prerender (app-shell sidebar renders on every page). Used `usePathname` (prerender-safe) + `window.location.search` read in the rAF mount effect instead — same result, zero hydration risk, all pages stay fully static.
- Expansion state lifted from per-node useState into ProjectTree (expandedPaths array + toggle callback prop-drilled through TreeNode/TreeChildren) so the whole set can be persisted; nodes restore expanded state after the mount rAF, so a stored tree re-expands one frame after hydration (no mismatch, brief collapsed-first paint).
</obstacles>

<log>
2026-09-08 — task created from user direction (click = open Entries, tree keeps state, Entries = active project's page).
2026-09-08 — done: tree-session.ts helper (3 sessionStorage keys), project nodes link /entries?project=<id> + record last active, active highlight derived from URL, expansion + Projects-section open persist per tab, Entries nav deep-links. lint 0 errors / 6 known warnings, tsc clean, build clean (8/8 static pages).
2026-09-08 — headless-Chrome smoke by orchestrator, 5/5 PASS: node links to /entries?project=<id>; entries page shows "Entries — Overmind"; active highlight (bg-primary/10) on the node; expansion survives navigation away and back; Entries nav deep-links to last active project.
</log>
