---
feature: 104
slug: materialization-off-write-path
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 104 — Materialization off the write path + honest timeout errors (Qwen friction #3)

> Grooming 2026-10-04 (user in session). Source: Qwen friction #3 (entry 500f8197) — a write that times out but still commits.

## Goal

`planning_patch_artifact` returned «HttpClient.Timeout of 10 seconds» while the patch HAD committed (read-back showed the new version). An agent re-sending on timeout creates double-version churn; not re-sending risks a lost write. The documented slow part of mutations is the inline materialization pass after each write.

## Decisions

- **Async materialization by default.** Mutations stage the mirror update into a bounded in-host queue (System.Threading.Channels + a BackgroundService consumer, sequential — the pass is already idempotent and per-run atomic per 033/074). The write returns when the DB transaction commits; the mirror catches up moments later. Event-driven trigger stays (mutation → enqueue), the manual `planning_materialize` tool stays SYNCHRONOUS (waiting for the report is its purpose), import stays synchronous.
- **Escape hatch:** `planning.materializationSync` setting (default `false` = async). One documented switch to restore inline behavior per project if a consumer ever needs the mirror updated inside the request.
- **Eventual consistency is documented, not hidden:** onboarding trigger-text and tool descriptions say «the write returns before the mirror updates; the board is the truth». 074 infra (lastAttempt/lastError/lastRun) is the observability surface — an async pass failure must land there, never vanish.
- **Honest timeout errors (adapter side).** On a mutation timing out / connection dying, the MCP adapter runs a verify probe: targeted read-back of the mutated artifact and a match of the mutation's `logNote` against `lastLogLine` (logNote is the durable marker). Probe hit → error text states «the mutation committed at vN — do not resend»; no trace → «no trace of the write — resend safe». Unknown/ambiguous → say so and recommend reading before resending. Applied to the shared mutation path (one handler), not per-tool copies.

## Non-goals

- Dropping the mirror or the event-driven trigger; changing artifact versioning semantics; distributed queues (single host); making reads wait for the mirror.

<log>
- 2026-10-04 [agent] — groomed with the user in session: async queue by default with a sync escape hatch, eventual-consistency documented, adapter verify-probe turns timeouts into committed/not-committed answers keyed by logNote.
- 2026-10-04 [agent] — validated with human proof: notification 3de6c9b0-5734-486b-b98b-9399a75bfa35 replied 2026-10-04 11:48:52 UTC
</log>