---
feature: 127
slug: project-tree-icons
status: done
priority: p1
version: 1
created: 2026-10-06
---

# Feature 127 — Project tree: purple project icons distinct from folder nodes

## Goal

In the project tree, PROJECT nodes must read as projects at a glance — a purple icon (brain-ish / mind metaphor, NOT a folder) — while directory nodes keep the plain folder look. Small visual feature, fast lane.

## Requirements (user's design, 2026-10-06)

- Project node icon: purple, brain-like or similar "mind" glyph — explicitly NOT the folder icon.
- Inactive project node: icon in a dark/muted purple; label stays as it renders today (grey/muted per current tree styles).
- Active project node: keeps its current color and display treatment — user is open to making the active state even brighter ("можно даже поярче") — spec proposes, human approves at result validation.
- Directory ("папки-папки") nodes: unchanged — current folder icon and styles.

## Non-goals

- No project-tree.tsx structural extract (F5 from the audit stays a separate follow-up) — this feature touches only the node icon/color layer.
- No tree behavior changes (expand/collapse, selection) — visual only.
- No new icon library dependency — the glyph comes from the existing icon approach in the tree (inline SVG / current glyph set); if a brain glyph doesn't exist in-house, pick the closest existing mind/projects glyph and flag the choice in obstacles for the human.

## Acceptance

- Project vs folder nodes are distinguishable by icon alone at a glance.
- Inactive/active project states follow the purple spec above.
- Web gates green (lint/tsc/tests/build); visual eyeball by the human at result validation.

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — armer notification f1d38300; result validated. Fast-lane feature closed (commit e5cab94): project nodes read as projects at a glance.
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
