---
feature: 091
slug: board-card-brief-phase-status
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Plan 091 — Board card: feature brief excerpt + ceremony phase status (spec/plan progress visible on the card)

3 tasks, backend → (web ∥ mcp) — one shared server-side derivation feeds both surfaces. No migrations.

## Key implementation decisions

- **One derivation function, server-side** (Infrastructure): `CeremonyPhase.Derive(feature)` + `BriefExcerpt(feature)` — consumed by the board payload (PlanningMapper/board read) and the task-queue projection alike. Phase machine per the spec table; excerpt = first non-empty `<goal>` paragraph capped ~150 chars with ellipsis, absent → field absent (null), no empty-string placeholders.
- Excerpt lands in `planning_task_queue` entries as an optional field (072 payload shape extends, contract tests pin the field presence/absence semantics).

## Task chain

1. **01 — Backend derivation.** CeremonyPhase + excerpt derivation in Infrastructure; board payload + queue entries carry them; edge-case unit matrix (no goal, no body, validated-but-draft, done card, long-goal cap, CJK/cyrillic cap safety).
2. **02 — Web board card (parallel with 03).** Card renders brief between priority/version row and task counter + phase label next to the status column; done cards follow existing done treatment; per-surface presence tests.
3. **03 — MCP queue excerpt (parallel with 02).** planning_task_queue entries carry excerpt + phase label; description updated; contract tests (field presence/absence pinned).
4. Closeout folds into task 03? No — keep it honest: task 04 **closeout** (consistency check both surfaces agree, architect light, result) after 02∥03.

## Verification

- Unit matrix for the phase machine + excerpt edge cases (task 01); presence tests per surface (02/03); full suites with real numbers; architect light in 04.

<log>
- 2026-10-04 [agent] — plan drafted after spec validation; 01 → (02∥03) → 04.
- 2026-10-03 [agent] — validated with human proof: notification 2e0dec65-6c4d-4865-a86e-cb927585f864 replied 2026-10-03 21:33:43 UTC
</log>