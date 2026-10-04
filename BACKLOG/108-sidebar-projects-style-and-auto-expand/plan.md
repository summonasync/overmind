---
feature: 108
slug: sidebar-projects-style-and-auto-expand
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 108 — Sidebar: Projects row/tree in the nav row style + auto-expand to the open project

> Grooming 2026-10-04. Web-only, 1 task (Simple-Medium path: coder → light review → result).

## Task chain

1. **01 — Row unification + auto-expand + tests + result.** Projects row into the standard nav row rhythm (border-l-2, integrated chevron affordance); project rows follow (indent for depth); auto-expand to the open project while ?project= is present (overrides saved collapsed state; saved state applies on non-project pages); active project highlighted in the primary tone; web tests: row classes, auto-expand on project route, saved-state interplay, selection highlight; result artifact.

## Verification

- Web tests on sidebar behavior; lint/tsc/node gates; light review.

<log>
- 2026-10-04 [agent] — plan groomed: single task; auto-expand overrides saved state while a project route is active (user default).
- 2026-10-04 [agent] — validated with human proof: notification dfb0d1ed-94c0-45c4-a2ae-67b6357759a5 replied 2026-10-04 12:00:47 UTC
</log>