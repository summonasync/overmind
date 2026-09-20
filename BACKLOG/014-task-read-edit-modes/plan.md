---
feature: 014
slug: task-read-edit-modes
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 014 — Task open modes: read view + edit

<approach>
One web-only task riding 012's shared toggle and xl modal: flip the task modal's default mode to read (Preview) for existing tasks, make the action bar mode-aware, done. The "two open variants" question from the feature body resolves to tabs-in-one-window (the toggle) — no second window.
</approach>

<contract>
- task-editor.tsx: showPreview initializes true for existing tasks (create stays Write); shared WritePreviewToggle unchanged.
- Preview mode: rendered body only (Markdown), Brief input hidden, close-only action bar. Write mode: current editor exactly.
- Nothing else changes.
</contract>

<tasks>
- [ ] 01-task-read-default — initial mode + mode-aware fields/action bar + verification → tasks/01-task-read-default.md
</tasks>

<risks>
- 023/03 runs in the planning slice in parallel (types/board/workspace — disjoint from task-editor.tsx, but shared npm runs); land after it or coordinate.
</risks>

<verification>
- lint + tsc + build green; manual sanity per the spec's acceptance ideas.
</verification>
