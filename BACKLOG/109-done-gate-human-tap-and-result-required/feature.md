---
feature: 109
slug: done-gate-human-tap-and-result-required
status: done
priority: p1
version: 1
created: 2026-10-04
---

# Feature 109 — Done gate: agent-side review→done needs a human tap; done (any door) requires a result artifact

<goal>
Done gets a human gate: an agent-side review→done flip requires the user's tap (notification-proof mechanics, same family as validation gates); a flip INTO review needs no tap (unchanged). AND any MCP-side done requires a result artifact to exist first — no result, no done.
</goal>

<context>
User decision 2026-10-04 in session: «нужен был мой тап, когда ТЫ переводишь из review в done; для перевода в review тап в ui не нужен. НО — любой перевод через mcp в done будет требовать записи в result». Today planning_set_feature_status (023/08) flips done with just a logNote — the «write the result first» discipline is cultural, not mechanical.
</context>

<open-questions>
Does the HUMAN door (UI click) also require the result artifact? Default: yes — the invariant is cleaner complete, and the UI click already means the human is present. Challengeable on grooming.
</open-questions>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
- 2026-10-05 [auto] — all tasks done + spec + plan validated → review
- 2026-10-05 [agent] — done via agent review — approved by human tap (notification f3ebfdf9, reply 2026-10-05 19:18:17 UTC)
- 2026-10-05 [agent] — claim released on done-close
</log>
