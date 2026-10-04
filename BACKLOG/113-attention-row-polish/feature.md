---
feature: 113
slug: attention-row-polish
status: done
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
- 2026-10-04 [agent] — Van by claude claimed the feature
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
- 2026-10-04 [agent] — agent by unknown released the claim
- 2026-10-04 [agent] — [agent] done by user approval — reply on result notification 231f7d76 (2026-10-04 18:42:32 UTC); result eca3d5c1 validated, light review PASS-WITH-NITS 0 blockers.
</log>
