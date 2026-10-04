---
feature: 051
task: 01
slug: entries-materialization
status: todo
review: light
created: 2026-10-04
brief: Materializer: ENTRIES/ write + remove + tombstone files + strategy + tests
---

# Task 01 — Materializer: ENTRIES write + remove paths

<goal>
Entries hit the disk: one entry, one file, strategy-respecting.
</goal>

<scope>
- ENTRIES/{type}/{kebab(title)}-{id8}.md (frontmatter id/type/title/tags/createdAt/updatedAt + body); idempotent passes (byte-identical no-ops).
- Remove on the next pass when the entry is gone (board = truth); tombstone entries materialize like any entry (file appears on 029 conversion).
- Rides the per-project strategy (off/on/archive).
- Tests: write shape, idempotency, remove path, tombstone file, strategy off skips.
</scope>

<acceptance-criteria>
- [ ] File shape + frontmatter pinned; idempotent re-pass — pinned.
- [ ] Remove path works (file gone when entry deleted) — pinned.
- [ ] Tombstone entry produces its file — pinned.
</acceptance-criteria>

<refs>
- Spec a87863c1; plan 46b7e368 (task 1); 029 entity.
</refs>