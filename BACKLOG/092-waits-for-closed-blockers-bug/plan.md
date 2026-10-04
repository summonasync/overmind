---
feature: 092
slug: waits-for-closed-blockers-bug
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 092 — Bug: "waits for" shows done/review blockers as waiting (feature page)

> Grooming 2026-10-04. Web-only, 1 task (Simple path: coder → light review → result; no architect pass for a one-component fix).

## Task chain

1. **01 — Fix + test + result.** `FeatureDependenciesPanel`: open blockers → «Waits for» group (danger chips, «N open» badge, X); closed blockers → «Cleared by» group (muted chips, line-through, link, X kept for cleanup); empty-waits-with-cleared renders «nothing open»; web test pins the split (open vs closed rendering, heading texts, badge counts); result artifact.

## Verification

- Web test on the split; lint/tsc/node-test gates; light review.

<log>
- 2026-10-04 [agent] — plan groomed: single task, Simple path, no architect.
- 2026-10-04 [agent] — validated with human proof: notification 59f6b203-1997-4e09-8407-b3d01683dfdb replied 2026-10-04 11:48:54 UTC
</log>