---
feature: 108
slug: sidebar-projects-style-and-auto-expand
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 108 — Sidebar: Projects row/tree in the nav row style + auto-expand to the open project

> Grooming 2026-10-04 (user in session, p1 intake same day).

## Goal

(a) The Projects row and the project rows inside the tree render in the same visual rhythm as Kits/Search/Settings rows (today Projects is special-cased: separate chevron button, different padding/hover). (b) When a project is open (any of its pages), the tree auto-expands to it and the project stays visibly selected — permanent «where am I».

## Decisions

- **Row style unification:** the Projects row becomes a standard nav row (border-l-2, same padding/hover/active treatment); the chevron integrates INTO the row (right-aligned affordance or click-zone on the caret glyph) instead of a visually separate button. Project rows inside the tree follow the same rhythm (indent instead of border-l for depth).
- **Auto-expand:** while the URL carries a project parameter, the tree is expanded to that project and the project row is highlighted as the selected one — REGARDLESS of the saved collapsed state (user decision default: auto-expand wins while a project route is active; the saved state applies again on non-project pages). Manual toggling still works on top.
- The active project's highlight uses the same primary tone as the nav rows' active state.

## Non-goals

- Multi-level nesting (projects are one level deep); reordering; the register modal flow (untouched).

<log>
- 2026-10-04 [agent] — groomed with the user in session: unified row rhythm with integrated chevron, auto-expand-to-open-project overrides saved state while a project route is active, primary-tone selection.
- 2026-10-04 [agent] — validated with human proof: notification dfb0d1ed-94c0-45c4-a2ae-67b6357759a5 replied 2026-10-04 12:00:47 UTC
</log>