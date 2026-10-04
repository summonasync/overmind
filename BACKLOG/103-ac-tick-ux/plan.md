---
feature: 103
slug: ac-tick-ux
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 103 — AC tick UX: echo what was ticked, un-tick op, index-base consistency (Qwen friction #2)

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03 (closeout folds the architect pass).

## Task chain

1. **01 — Core + service.** `TickAc` goes 1-based; new `UntickAc` (text primary / 1-based index, already-unticked no-op, TickItemNotCheckable for markerless items); `FormatAcItems` renders 1-based; patch result carries `tickedIndex`/`tickedText` echo. Matrix tests: tick by index/text (1-based), retick no-op, untick paths, markerless 400, out-of-range 400 with 1-based listing.
2. **02 — Doors + docs.** MCP: acIndex param 1-based, untick operation, description states «1-based, matches the materialized mirror; acText is the recommended selector» verbatim; HTTP door + echo contract; description pins updated (0-based pins rewritten, no shims); contract tests both doors.
3. **03 — Closeout.** Consistency pass (echo parity across doors), architect light, result; release-notes line for the batch: «acIndex is now 1-based — breaking, loud».

## Verification

- Matrix tests (01), door contract tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear, breaking-change doc line rides the batch release notes.
- 2026-10-04 [agent] — validated with human proof: notification d9415511-7925-4205-b641-02524360c2e3 replied 2026-10-04 11:48:51 UTC
</log>