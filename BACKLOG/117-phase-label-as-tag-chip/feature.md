---
feature: 117
slug: phase-label-as-tag-chip
status: done
priority: p0
version: 1
created: 2026-10-04
---

# Feature 117 — Board card: phase label as tag chip after the slug

<goal>
On the board card, the ceremony phase label («plan validated», «in progress», …) currently renders between the title and the status column. Move it to sit right after the feature slug and style it like our tag chips.
</goal>

<context>
User dogfood feedback 2026-10-04: «статусы plan validated и тд — она прям между тайтлом и статусом. Я бы хотел её там где slug, после него — и как-то как наши тэги визуально». 091 introduced the derived phase label on cards; 065 provides the shared tag/badge primitives.
</context>

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 01 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — armer notification 6c1e4049; result validated in the final batch sitting. Phase chip closed (2f4cac2 + 1133e30).
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
