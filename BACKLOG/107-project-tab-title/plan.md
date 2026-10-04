---
feature: 107
slug: project-tab-title
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 107 — Tab title shows the open project («{Project} — Overmind»)

> Grooming 2026-10-04. Web-only, 1 task (Simple path: coder → light review → result).

## Task chain

1. **01 — Single title owner + tests + result.** One hook computes document.title = f(projectName, awaitingCount) — awaiting composes over the project base; name from the 101 resolver over the ?project= param; graceful «Overmind» fallback; replace the mount-once capture (microbatch #134 resolved); web tests: project route title, fallback, awaiting composition; result artifact.

## Verification

- Web tests on title computation; lint/tsc/node gates; light review.

<log>
- 2026-10-04 [agent] — plan groomed: single task, Simple path.
- 2026-10-04 [agent] — validated with human proof: notification 7a943573-4bd9-43e5-80ca-9462bfbe3402 replied 2026-10-04 12:00:47 UTC
</log>