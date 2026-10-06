---
feature: 095
task: 02
slug: os-desktop-notifications
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Closeout: surface consistency, architect light, result + click-check
---

# Task 02 — Closeout: consistency + architect + result

<goal>
Close the wave: one event, three surfaces, no duplicate noise.
</goal>

<scope>
- Consistency pass: bubble (093) vs toast vs title attention over one event source.
- Architect light; result with post-release click-check (reply to an approval from another window: toast + chirp + favicon/title fire; click focuses and deep-links).
</scope>

<acceptance-criteria>
- [x] Consistency recorded; architect verdict recorded; result drafted with click-check.
</acceptance-criteria>

<refs>
- Spec 12623237; plan 6b95d68b (task 2).
</refs>

<log>
- 2026-10-06 [agent] — taken by orchestrator — closeout: consistency pass, architect light, result (final p1 wave)
- 2026-10-06 [agent] — closeout complete: consistency pass done (single source query; awaiting-predicate divergence bubble readAt vs attention hiddenAt documented in the result per architect W2; no duplicate noise — firedIds dedup + max() title), architect light APPROVED-WITH-WARNINGS with W1 fixed in-tree (window.location.assign per the static-export ADR) and W2 documented (audits/architect-095.md), result 297ae98c drafted with the 7-step permission click-check. Feature goes to review; done-flip waits on the human tap.
</log>
