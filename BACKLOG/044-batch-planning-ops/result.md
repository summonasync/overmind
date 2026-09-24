---
feature: 044
slug: batch-planning-ops
status: draft
created: 2026-09-24
---

# Result 044 — Batch planning ops: создать N артефактов / двинуть N статусов одним вызовом

<goal>
Delivery record for 044-batch-planning-ops: planning_upsert_tasks shipped end-to-end with all-or-nothing semantics.
</goal>

<context>
OM-8 from the 2026-09-23 field run: 5 tasks cost 5 upsert + 5 set_status calls and per-call transition evaluation produced the OM-5 phantom review flicker. The batch surface closes both.
</context>

<scope>
Shipped across Core → Infrastructure → Host API → MCP: planning_upsert_tasks(projectId, featureSlug, tasks[...]) — items staged in one change tracker, single SaveChangesAsync (exception before persist = whole batch rolls back), ONE materialization sync and ONE auto-transition evaluation after the batch (the 057 reopen-flicker root). Item-indexed validation errors on both doors (MCP: tasks[i]: without an HTTP call; API: 400 with tasks[i] keys, zero side effects). Atomicity documented in the tool description. Concurrent unique-violation maps to 409 TaskBatchConcurrentWrite with a retry hint. SessionBootstrapPrompt teaches the write-cost tier.
</scope>

<acceptance-criteria>
- [x] 5 tasks via one call; feature log shows a single transition evaluation (pinned: UpsertTasks_FiveTasksInOneCall + reopen case UpsertTasks_CreateFromReview_SingleReopenLineNotOnePerTask)
- [x] Validation failure names the offending item index (three levels: MCP pre-call, API 400, service detail)
- [x] dotnet build passes (0 warnings)
</acceptance-criteria>

<refs>
audits/review-044-01.md (light review: APPROVED WITH WARNINGS → fix round W1/W2/S1 → re-check APPROVED); 057-review-autotransition-flicker (root closed); task 880827ec (done, log carries the fix round); open follow-up: microbatch #62 (S2 update-in-place duplication), documented semantics: Updated counts items, not unique artifacts
</refs>

<obstacles>
FluentValidation 12.1.1: RuleForEach+SetValidator silently skips null items AND the {Index} placeholder is not substituted — static message with index-in-key is the correct form, not a compromise.
</obstacles>

<log>
- 2026-09-24 [agent] — result recorded after APPROVED re-check (fix round W1 null-item guard, W2 end-to-end concurrent-write test via CompetingWriterInterceptor, S1 TryGetInt32 indexed parse)
</log>