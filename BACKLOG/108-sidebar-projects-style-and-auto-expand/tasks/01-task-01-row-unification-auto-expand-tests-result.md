---
feature: 108
task: 01
slug: sidebar-projects-style-and-auto-expand
status: done
review: light
created: 2026-10-04
brief: Web: nav-row style parity + auto-expand to open project + tests + result
---

# Task 01 — Row unification + auto-expand + tests + result

<goal>
The Projects section stops being special-cased and always shows where you are.
</goal>

<scope>
- Projects row into the standard nav row rhythm (border-l-2, same padding/hover/active); chevron integrated into the row (right-aligned affordance), not a separate button; project rows follow (indent for depth).
- Auto-expand: while ?project= is present the tree expands to that project and the row is highlighted in the primary tone — overriding the saved collapsed state; saved state applies again on non-project pages; manual toggle still works.
- Web tests: row classes, auto-expand on project route, saved-state interplay, selection highlight.
- Result artifact.
</scope>

<acceptance-criteria>
- [x] Row style parity pinned; integrated chevron a11y (aria-expanded) kept.
- [x] Auto-expand overrides saved state while a project route is active — pinned; saved state restores on non-project pages — pinned.
- [x] Gates green; result drafted.
</acceptance-criteria>

<refs>
- Spec 7f99d030; plan deff9984; app-sidebar.tsx; tree-session.
</refs>

<obstacles>
- A project whose diskPath sits outside every registered tree root cannot be auto-expanded: the tree only renders root subtrees, so the row is simply not visible. Degrades silently (empty ancestor chain), no error path added.
- The old pathname gate (/entries, /dashboard) was removed per spec ("while the URL carries a project parameter"). Today every page using ?project= treats it as a project id (/, /dashboard, /entries, /issues), so no behavioral conflicts; if a future page reuses ?project= for something else, the sidebar will highlight/expand on it too.
- Toggles on project pages: the auto-expanded chain itself stays visit-local (openOverride on the section, routeCollapsed on the tree), but a manual toggle of any path OUTSIDE the auto chain still persists to sessionStorage even on a project route. Review correction: the earlier claim "persistence happens only off project routes" was half-true; the real invariant is "auto-chain overrides are visit-local, manual toggles persist everywhere". Accepted as designed.
- The URL is read once in a mount effect; client-side router.replace navigations (the /planning redirect) are not seen until a full load. Pre-existing class of behavior (activeProjectId was mount-only before), recorded for the ledger (#236).
- Auto-expand resolves the project through useProjectList().diskPath; until the projects query resolves on a cold load the tree renders un-expanded for a frame — React Query cache makes this invisible in practice.
</obstacles>

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching web-coder (sonnet): row unification + auto-expand + tests + result
- 2026-10-05 [agent] — web-coder: nav-row parity for Projects row + integrated right-aligned chevron; project leaf rows on the nav rhythm; auto-expand of section and tree chain to ?project= (overrides saved state, visit-local toggles); pure projectAncestorPaths helper; 18 new tests; gates green — lint 0 errors, tsc clean, tests 200/200, build ok.
- 2026-10-05 [agent] — light review PASS-WITH-NITS 0 blockers: row classes byte-identical to nav rows, union expand logic (saved ∪ auto − routeCollapsed) verified across all brief cases, no save-loop, keys/a11y clean. Obstacles corrected per review nit 2 (persistence invariant was half-true — real rule: auto-chain visit-local, manual toggles persist everywhere; accepted as designed). Nits to microbatch: #236 client-nav blindness (URL read in mount effect, router.replace not seen), #237 empty ?project= desync between has() and get() checks, #238 chevron hit area p-0.5. Gates re-run by orchestrator: lint 0e/6 pre-existing, tsc clean, 200/200, build ok.
</log>