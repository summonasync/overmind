---
feature: 108
slug: sidebar-projects-style-and-auto-expand
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 108 — Sidebar: Projects row/tree in the nav row style + auto-expand to the open project

<goal>
Sidebar Projects section: (a) the Projects row and the project rows render in the SAME visual style as Kits/Search/Settings rows (no special-cased chevron look); (b) when a project is open (dashboard/entries/issues/settings of that project) the tree auto-expands to it and the project stays visibly selected — permanent «where am I».
</goal>

<context>
User request 2026-10-04 (p1 intake). Today app-sidebar.tsx special-cases Projects: separate chevron button, different hover/padding rhythm vs the plain border-l-2 rows. ProjectTree expansion state lives in tree-session (manual only) — navigation into a project does not expand the tree, and the active project is not surfaced when the section is collapsed.
</context>

<open-questions>
Expand-on-navigation semantics: auto-expand overrides the saved collapsed state while a project is open, or only expands once per session? Default: auto-expand while a project route is active (save-state applies again when the user leaves to non-project pages). Confirm on grooming.
</open-questions>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
