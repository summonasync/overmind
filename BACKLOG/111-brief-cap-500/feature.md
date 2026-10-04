---
feature: 111
slug: brief-cap-500
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 111 — Board card brief cap: 150 → 500 text elements (user dogfood feedback)

<goal>
Board card brief cap raised from 150 to 500 text elements (user dogfood feedback 2026-10-04: «карточки будут выглядеть только лучше») — more goal text visible on the card without opening the feature.
</goal>

<context>
The 150-cap came from spec 091 (grapheme-safe StringInfo cap, «…» on overflow, null on absent goal). The same derivation feeds board cards and planning_task_queue briefExcerpt. User field feedback after the release: 150 is too short, 450–600 reads better — taking 500 as the single constant. No format change: first non-empty <goal> paragraph, StringInfo text elements (surrogate pairs never split), «…» on overflow.
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
