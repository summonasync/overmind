---
feature: 104
slug: materialization-off-write-path
status: draft
created: 2026-10-04
---

# Spec 104 — Materialization off the write path + honest timeout errors (Qwen friction #3)

> Grooming 2026-10-04 (user in session). Source: Qwen friction #3 (entry 500f8197) — a write that times out but still commits.

## Goal

`planning_patch_artifact` returned «HttpClient.Timeout of 10 seconds» while the patch HAD committed (read-back showed the new version). An agent re-sending on timeout creates double-version churn; not re-sending risks a lost write. The documented slow part of mutations is the inline materialization pass after each write.

## Decisions

- **Async materialization by default.** Mutations stage the mirror update into a bounded in-host queue (System.Threading.Channels + a BackgroundService consumer, sequential — the pass is already idempotent and per-run atomic per 033/074). The write returns when the DB transaction commits; the mirror catches up moments later. Event-driven trigger stays (mutation → enqueue), the manual `planning_materialize` tool stays SYNCHRONOUS (waiting for the report is its purpose), import stays synchronous.
- **Escape hatch:** `planning.materializationSync` setting (default `off` = async). One documented GLOBAL switch (v1 — per-project override is a non-goal; the original «per project» wording here was inaccurate, recorded by architect-104 N1) to restore inline behavior if a consumer ever needs the mirror updated inside the request.
- **Eventual consistency is documented, not hidden:** onboarding trigger-text and tool descriptions say «the write returns before the mirror updates; the board is the truth» (and say INLINE when the sync flag is on). 074 infra (lastAttempt/lastError/lastRun) is the observability surface — an async pass failure must land there, never vanish; a shutdown-cancelled pass is re-run by the worker, not dropped.
- **Honest timeout errors (adapter side).** On a mutation timing out / connection dying, the MCP adapter runs a verify probe: targeted read-back of the mutated artifact and a match of the mutation's `logNote` against the TAIL of the artifact's log (logNote is the durable marker). Probe hit → error text states «the mutation committed at vN — do not resend»; log tail without the marker → «no trace — resend safe»; probe impossible (no descriptor) or unreadable (section-less log, failed read-back) → «unknown — read before resending». Applied to the shared mutation path (one handler), not per-tool copies.

## Non-goals

- Dropping the mirror or the event-driven trigger; changing artifact versioning semantics; distributed queues (single host); making reads wait for the mirror.

<log>
- 2026-10-04 [agent] — groomed with the user in session: async queue by default with a sync escape hatch, eventual-consistency documented, adapter verify-probe turns timeouts into committed/not-committed answers keyed by logNote.
- 2026-10-04 [agent] — validated with human proof: notification 3de6c9b0-5734-486b-b98b-9399a75bfa35 replied 2026-10-04 11:48:52 UTC
- 2026-10-05 [agent] — architect-104 N1 honesty pass: escape hatch is GLOBAL v1 (original «per project» wording was wrong — no per-project override shipped); probe wording aligned with the implemented log-tail match and unknown-for-section-less reads (review-104-02 W1/W2); worker re-runs shutdown-cancelled passes (architect-104 W1). Frozen artifact demoted to draft per the editing rule; re-validation rides the user's return tap.
- 2026-10-05 [agent] — architect-104 honesty pass (N1 + wording alignment): escape hatch documented as global v1, probe semantics match the implemented log-tail match and unknown-for-section-less behavior, shutdown re-run recorded. Demoted from validated per the frozen-artifact editing rule — re-validation rides the user's return tap together with the result.
- 2026-10-05 [auto] — artifact demoted: validated → draft
</log>