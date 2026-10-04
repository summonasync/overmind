---
feature: 113
slug: attention-row-polish
status: draft
created: 2026-10-04
---

# Plan 113 — Attention rows: dashboard number style + clickable hover-highlighted title

> Grooming 2026-10-04. 1 task (Simple path: coder → light review → result; no architect).

## Task chain

1. **01 — Number badge + clickable title + tests + result.** ceremonies-section.tsx: number through the dashboard number treatment (FeatureNumberBadge), title → `<a>` with hover highlight (board-card link style), «Open →» kept unless visually colliding; web test pins the number treatment and title-link href; result artifact.

## Verification

- Web test on row rendering; lint/tsc/node gates; light review.

<log>
- 2026-10-04 [agent] — plan groomed: single task, one component.
</log>