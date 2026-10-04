---
feature: 111
slug: brief-cap-500
status: draft
created: 2026-10-04
---

# Result 111 — Board card brief cap: 150 → 500 text elements (user dogfood feedback)

## Outcome

The brief excerpt cap moved 150 → 500 text elements (`CeremonyPhasePlanner.ExcerptCap`): board cards and `planning_task_queue` briefExcerpt (same derivation, 091 parity intact) now carry up to 500 characters of the first `<goal>` paragraph. All 091 safety properties unchanged: StringInfo text elements (surrogate pairs never split), «…» on overflow, null on absent goal/body/blank goal. No line-clamp introduced (091 «bounded upstream, plain <p>» stands — the bound moved). The `planning_task_queue` description's «~150 chars» updated to «~500 chars».

## Verification

- 091 cap matrix re-pinned and green: 500/501 boundary (cyrillic), CJK surrogate-pair boundary at 500 ideographs + ellipsis (high surrogate never travels alone), verbatim-below-cap case untouched.
- Board+queue parity (Api, real doors) green: 2/2.
- Affected suites: Infrastructure planner 19/0, Api ceremony context 2/0; build 0/0. Full-suite gate rides the next pipeline checkpoint (071/01 shares the solution).

## Deviations

None — the change is the constant plus re-pins, per spec.

## Post-release click-check

A feature with a long goal (>500 chars) shows a ~500-char brief with «…» on the card and in the queue.

<log>
- 2026-10-04 [agent] — executed by the orchestrator (one-constant change): cap 150→500, matrix re-pinned at 500/501, tool description updated; planner matrix 19/0, Api parity 2/0.
</log>