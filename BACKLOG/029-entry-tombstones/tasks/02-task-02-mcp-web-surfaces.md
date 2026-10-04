---
feature: 029
task: 02
slug: entry-tombstones
status: todo
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
- MCP delete_entry gains `reason`; list filter type=tombstone verified through the adapter.
- Web: tombstone in type filters, muted tone for rows, create flow allows the type, tombstone rows show provenance/reason.
- Tests: adapter contract, web rendering states.
</scope>

<acceptance-criteria>
- [ ] delete_entry reason — contract-tested.
- [ ] Web filter/tone/create states — pinned.
</acceptance-criteria>

<refs>
- Spec 9a3cef1f; plan 241a8ad0 (task 2).
</refs>