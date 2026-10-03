---
feature: 091
task: 04
slug: board-card-brief-phase-status
status: todo
review: light
created: 2026-10-03
depends_on: 02, 03
brief: Consistency check + architect light + result with release click-check
---

# Task 04 — Closeout: consistency + architect + result

<goal>
Wave closeout per the batch strategy: no live dogfood.
</goal>

<scope>
- Consistency check: board card and queue agree on labels (same derivation — verify no divergence).
- Architect light (orchestrator dispatches separately).
- Result artifact: outcome, deviations, gates evidence, post-release click-check (board card of a drafting feature, of an in-progress feature, queue entry excerpt in a real MCP session).
- Board work: logs, obstacles, AC ticks, status review.
</scope>

<anti-scope>
No live dogfood (batch after release). No product code beyond what the architect forces.
</anti-scope>

<constraints>
English-only; architect brief handled by the orchestrator.
</constraints>

<acceptance-criteria>
- [ ] Consistency check done (board vs queue agree)
- [ ] Full dotnet suite + web gates green on the final tree
- [ ] Architect light done (orchestrator), blockers fixed or waived
- [ ] Result artifact drafted with the release click-check list
</acceptance-criteria>

<refs>
- Spec: d066ec12.
- Tasks 1–3 — the code this closeout verifies.
</refs>

<obstacles></obstacles>

<log></log>