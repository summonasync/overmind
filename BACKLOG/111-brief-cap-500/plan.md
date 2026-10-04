---
feature: 111
slug: brief-cap-500
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 111 — Board card brief cap: 150 → 500 text elements (user dogfood feedback)

> Grooming 2026-10-04. One-constant change, 1 task (Simple path: coder → light review → result; no architect).

## Task chain

1. **01 — Cap constant + re-pin + result.** `BriefCapLength` 150 → 500 in CeremonyPhasePlanner; the 091 cap matrix re-pinned (500/501 boundary, cyrillic long goal, surrogate pairs, «…» overflow, null contract untouched); contract tests that assert excerpt shapes updated where they hardcoded the cap; result artifact.

## Verification

- Planner matrix + contract pins green; full affected suites; light review.

<log>
- 2026-10-04 [agent] — plan groomed: single task, one constant, matrix re-pin.
- 2026-10-04 [agent] — validated with human proof: notification e21a3661-fa22-428f-961c-159ee83f044b replied 2026-10-04 16:36:37 UTC
</log>