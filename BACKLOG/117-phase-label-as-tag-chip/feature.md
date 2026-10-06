---
feature: 117
slug: phase-label-as-tag-chip
status: ready
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
</log>
