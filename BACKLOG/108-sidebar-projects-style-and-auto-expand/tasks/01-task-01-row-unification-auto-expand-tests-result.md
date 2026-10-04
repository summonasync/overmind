---
feature: 108
task: 01
slug: sidebar-projects-style-and-auto-expand
status: todo
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
- [ ] Row style parity pinned; integrated chevron a11y (aria-expanded) kept.
- [ ] Auto-expand overrides saved state while a project route is active — pinned; saved state restores on non-project pages — pinned.
- [ ] Gates green; result drafted.
</acceptance-criteria>

<refs>
- Spec 7f99d030; plan deff9984; app-sidebar.tsx; tree-session.
</refs>