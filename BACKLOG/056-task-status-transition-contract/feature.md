---
feature: 056
slug: task-status-transition-contract
status: done
priority: p1
version: 1
created: 2026-09-23
---

# Feature 056 — Task status transitions: free-form graph undocumented + done with unticked AC passes silently (OM-4)

Field report OM-4 (FEATURELIST/overmind-features-2026-09-23.md): all five parallel agents independently did `todo → done` in a single `planning_set_status` call, skipping inProgress/review. The tool description lists allowed statuses but never says whether the transition graph is free-form — agents expected rejection and learned the truth empirically.

Tasks 12–15 were also closed `done` with 8–12 unticked `<acceptance-criteria>` items each — no warning.

Requested fix: one description line on `planning_set_status` documenting transition freedom (or the actual graph), plus a non-blocking `warnings` array in the response when a task moves to `done` with unticked AC items (e.g. `"8 of 8 acceptance items unticked"`). Warning, not error: migrations and manual overrides must still pass.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
- 2026-09-24 [agent] — Closing by user mandate 2026-09-24: single task done with light review PASS-WITH-NITS (audits/review-056-01.md). This closes the 2026-09-23 field wave: 054, 055, 056, 057, 059 done; 062 done; 048 awaiting its prod-measurement AC on the next deploy.
</log>
