---
feature: 110
slug: ready-means-ready-status-honesty
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 110 — Status honesty: spec/plan grooming keeps the feature ready; inProgress only when a task is taken

> Grooming 2026-10-04. 2 tasks, 01 → 02.

## Task chain

1. **01 — Transition rewrite + data-fix.** Auto-transition «artifact create → inProgress» removed (ceremony writes keep/make the feature ready); first task-take (either door) promotes ready → inProgress with an [auto] line; review-rollback path untouched; one-time data-fix (inProgress with no taken task → ready, [auto] log per feature) applied on startup migration; tests: ceremony keeps ready, take promotes, data-fix matrix (no tasks / all todo / one taken), review paths.
2. **02 — Closeout.** Consistency: surfaces reading status (board filters, 091 phase labels, claim UI) verified against the new semantics; docs lines where «artifact create → inProgress» is described (quickstart, skill); architect light; result with post-release click-check (groom a fresh feature → stays ready; take task 01 → inProgress).

## Verification

- Transition/data-fix tests (01), gates; architect light (02).

<log>
- 2026-10-04 [agent] — plan groomed: 2 tasks; no auto-demote (recorded challengeable default in the spec).
- 2026-10-04 [agent] — validated with human proof: notification 4f14cb8d-22c2-4937-aa32-4547980fab75 replied 2026-10-04 12:03:14 UTC
</log>