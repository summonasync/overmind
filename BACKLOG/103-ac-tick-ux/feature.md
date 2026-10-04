---
feature: 103
slug: ac-tick-ux
status: review
priority: p1
version: 1
created: 2026-10-03
---

# Feature 103 — AC tick UX: echo what was ticked, un-tick op, index-base consistency (Qwen friction #2)

<goal>
AC ticking is silently wrong-prone and unrecoverable (Qwen Code friction log, item 2, observed 2026-10-03): `acIndex` is 0-based while every human-facing rendering (materialized mirror, 400 diagnostics) is 1-based — an agent ticking "item 3" hits the neighbour and the write SUCCEEDS; and there is no un-tick, so repair costs a full section re-send.
</goal>

<requirements>
1. `acText` exact-match is already the primary selector (090) — keep it; document it as the recommended path in tool descriptions.
2. Echo the ticked item back: patch response carries the item's index AND text — the caller sees WHAT was ticked (would have caught the observed mis-tick instantly).
3. Un-tick operation: a patch mode clearing an item's checked state (by acText or 1-based-visible index) — repair without section re-send.
4. Index-compat decision at spec: either move acIndex to 1-based matching the rendering (breaking change — loud, documented) or keep 0-based but echo + teach acText-primary. Decide by weighing the installed base of agents.
</requirements>

<non-goals>
- AC data-model changes beyond the clear-state flag.
</non-goals>

<refs>
- Friction log entry 500f8197 (item 2, observed payload included).
- 090 R1 (acText already exists).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-04 [agent] — Van by claude claimed the feature
- 2026-10-04 [auto] — all tasks done + spec + plan validated → review
- 2026-10-04 [agent] — agent by unknown released the claim
</log>
