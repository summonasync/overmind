---
feature: 104
slug: materialization-off-write-path
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 104 — Materialization off the write path + honest timeout errors (Qwen friction #3)

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03.

## Task chain

1. **01 — Backend async pass.** Channel + BackgroundService consumer (sequential, bounded); mutations enqueue instead of awaiting the pass (`PlanningServiceGuard.SyncMaterializationAsync` becomes enqueue-or-inline per `planning.materializationSync`, default async); manual `planning_materialize` and import stay synchronous; async failure lands in 074's lastAttempt/lastError (pinned); tests: write returns before the pass runs, queue drains in order, sync-flag restores inline behavior, docs strings updated (eventual consistency wording).
2. **02 — Adapter honest timeouts.** Shared mutation-path handler: on timeout/dead connection run the verify probe (read-back + logNote match against lastLogLine) and surface «committed at vN — do not resend» / «no trace — resend safe» / «unknown — read before resending»; stub contract tests for all three probe outcomes.
3. **03 — Closeout.** Consistency pass (descriptions/onboarding wording vs behavior), architect light, result with post-release click-check (mutate → observe mirror catching up; kill the pass → lastAttempt shows the failure).

## Verification

- Queue/flag tests (01), stub contract tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear; manual materialize/import stay synchronous by design.
- 2026-10-04 [agent] — validated with human proof: notification 3de6c9b0-5734-486b-b98b-9399a75bfa35 replied 2026-10-04 11:48:52 UTC
</log>