---
feature: 029
task: 01
slug: entry-tombstones
status: done
review: light
created: 2026-10-04
brief: Core+Infra: Tombstone type, delete conversion with reason/provenance/fenced body, tests
---

# Task 01 — Core + Infra: tombstone type + delete conversion

<goal>
The fifth entry type and the delete-that-buries.
</goal>

<scope>
- EntryType.Tombstone (full citizen: filters, index, validation).
- DELETE /entries/{id} converts: original row + versions go; tombstone born with original title + «(deleted)», original type, deletion date, door+actor provenance, optional reason (new optional field on the delete path), final body in a fenced block.
- Tombstone delete creates no second tombstone.
- Tests: conversion matrix (with/without reason, agent/human provenance), type filter, no-recursion rule.
</scope>

<acceptance-criteria>
- [x] Delete converts with reason, provenance, fenced final body — pinned.
- [x] Tombstone delete does not recurse — pinned.
- [x] type=tombstone filter and indexing work — pinned.
</acceptance-criteria>

<refs>
- Spec 9a3cef1f; plan 241a8ad0 (task 1).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Runs solo (dotnet lane free).
- 2026-10-05 [agent] — Coding round complete (dotnet-coder, sonnet): EntryType.Tombstone (appended at enum end — DB stores ints), CreateTombstone factory with recursion+door guards, delete conversion in EntriesApi.Delete (the single choke point both doors share; no separate entry service exists — handler-direct is the documented norm for non-planning contexts), optional reason as query param, fence sized past content's own backtick runs, FTS+Qdrant pick the grave up (old vector removed), tombstone delete = plain delete (no recursion). Delete response contract 204→200 (additive, both clients tolerant — pins updated). Known deferrals to task 02: delete_entry description still lies, list_entries NormalizeType/ReadableEntryTypes still cut type=tombstone at the MCP door (service/HTTP level pinned). Gates re-verified by orchestrator (honesty check post-105): build 0w, Core 344 (+6), Infra 639, Api 630 (+5), Mcp 455, format clean. Checkpoint ad68f6e. Dispatching light review.
- 2026-10-05 [agent] — [agent] Light review PASS-WITH-NITS (audits/review-029-01.md; gates re-run by reviewer, numbers match honest orchestrator run: Core 344→345, Infra 639, Api 630, Mcp 455). W1 fixed in-tree (f29bdb7): grave fence sized past backtick runs in the reason too (reason renders right above the fence). W2 closed on the board: task 02 scope+AC patched to own write_entry tombstone admission + list_entries NormalizeType + honest delete_entry description (was an ownerless spec gap). Reviewer verified: conversion atomic (single SaveChanges), RemoveAsync targets the ORIGINAL id's vector, recursion cut at the API branch before the entity guard, 204→200 tolerant in both client parsers, KitImporter non-conversion correct. N1 (reason unbounded) + N2 (title 510 over the 500 rule, TEXT column) → microbatch. All 3 AC ticked. Closing task.
</log>
