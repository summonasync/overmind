---
feature: 113
slug: attention-row-polish
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 113 — Attention rows: dashboard number style + clickable hover-highlighted title

<goal>
Attention page ceremony rows get the dashboard's number style and a clickable, hover-highlighted title (user dogfood feedback 2026-10-04 on the released 087 page): «номер фичи — стиль с дашборда; тайтл при hover подсветить; открыть по клику на тайтл».
</goal>

<context>
ceremonies-section.tsx renders the feature number as a raw mono span (not the dashboard's #065-style feature number badge), the title is a plain non-clickable span without hover treatment — the only way in is the «Open →» link at the row end.
</context>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
