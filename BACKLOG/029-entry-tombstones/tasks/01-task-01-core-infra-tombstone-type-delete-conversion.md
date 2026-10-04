---
feature: 029
task: 01
slug: entry-tombstones
status: todo
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
- [ ] Delete converts with reason, provenance, fenced final body — pinned.
- [ ] Tombstone delete does not recurse — pinned.
- [ ] type=tombstone filter and indexing work — pinned.
</acceptance-criteria>

<refs>
- Spec 9a3cef1f; plan 241a8ad0 (task 1).
</refs>