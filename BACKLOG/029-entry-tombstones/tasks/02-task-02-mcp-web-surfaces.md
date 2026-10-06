---
feature: 029
task: 02
slug: entry-tombstones
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: MCP reason param + web filter/tone/create states + tests
---

# Task 02 — MCP + Web surfaces

<goal>
Both surfaces see tombstones as first-class.
</goal>

<scope>
- MCP delete_entry gains `reason`; list filter type=tombstone verified through the adapter (NormalizeType/ReadableEntryTypes admit tombstone — review-029-01 W2).
- MCP write_entry admits type=tombstone (spec keeps hand-made burials open) and the delete_entry description stops claiming "no tombstones, no undo".
- Web: tombstone in type filters, muted tone for rows, create flow allows the type, tombstone rows show provenance/reason.
- Tests: adapter contract, web rendering states.
</scope>

<acceptance-criteria>
- [x] delete_entry reason — contract-tested.
- [x] write_entry type=tombstone + list filter admitted through the adapter — contract-tested.
- [x] Web filter/tone/create states — pinned.
</acceptance-criteria>

<refs>
- Spec 9a3cef1f; plan 241a8ad0 (task 2).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Taken by orchestrator (task 01 done). Scope expanded per review-029-01 W2: MCP write_entry tombstone admission + list_entries NormalizeType + honest delete_entry description now owned here. Split by lane: dotnet-coder on the MCP half first, then web-coder on the web half (single task, two lanes sequential).
- 2026-10-05 [agent] — MCP half complete (dotnet-coder, sonnet; gates re-verified by orchestrator — build 0w, Mcp 459 (+4), format clean; checkpoint 6480a67): delete_entry optional reason (blank → no query param), tombstone admitted in list (NormalizeType/ReadableEntryTypes + upstream query) and write_entry (hand-made burials), delete_entry description states the bury-not-erase truth. Echo unchanged ({id,type,deleted} — no tombstone id, agent finds it via type=tombstone; reviewer to judge). Web half in flight (web-coder).
- 2026-10-05 [agent] — Web half complete (web-coder, sonnet; gates re-verified by orchestrator — lint 0e/6w pre-existing, tsc 0, tests 149 (+11), build 12/12; checkpoint ee8b539): tombstone in ENTRY_TYPES (filters/breakdown/editor), muted tone + Skull icon, grave rows tinted, delete dialog tells the bury-truth, deleteEntry typed for 200+body/204, ApiError ctor refactor behavior-identical for node testability. DECLARED DEVIATION for review: no reason input in the web delete dialog — spec decision mentions "confirm в UI" offers reason, plan task-02 row didn't list it, coder took the simple version per dispatch directive; backend ?reason= is ready. Both halves on the tree — dispatching parallel light reviews (dotnet for MCP half, web for web half).
- 2026-10-05 [agent] — [agent] Both halves reviewed PASS-WITH-NITS (audits/review-029-02-mcp.md, audits/review-029-02-web.md; gates re-run by reviewers AND orchestrator: Mcp 459, web 150, tsc 0, build 12/12, lint 0e/6w pre-existing). Fix round d552c2c: MCP W1 (escape pin now carries &/%/# — space-only reason passed even without EscapeDataString) + Canned fixture line layout; web W1 (reason input in the delete dialog for live entries — spec gap closed, graves excluded deliberately, deleteEntry(id, reason?) with encodeURIComponent). Web W2 (contrast 3.76:1 muted title) → microbatch: systemic --color-muted token weakness of the whole app, not wave-scoped — a design call for the human. MCP N2 (update_entry description omits tombstone) → task 03 closeout. Echo minimalism judged sound by both reviewers. All 3 AC ticked. Closing task.
</log>
