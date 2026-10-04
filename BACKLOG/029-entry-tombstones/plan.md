---
feature: 029
slug: entry-tombstones
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 029 — Entry tombstones: тип записи + автозахват при удалении + список для агентов перед работой

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03.

## Task chain

1. **01 — Core + Infra.** EntryType.Tombstone; DELETE converts: original row + versions go, tombstone born (original title + «(deleted)», original type, deletion date, door+actor provenance, optional reason from the request, final body fenced); `reason` optional on the delete path (domain + service); tests: conversion matrix (with/without reason, both doors provenance), tombstone is a full citizen of type filters/indexes.
2. **02 — MCP + Web.** delete_entry gains `reason`; list filter type=tombstone works; web: type in filters, muted tone for tombstone rows, create flow allows the type, tombstone delete creates no second tombstone; tests.
3. **03 — Closeout.** Onboarding quickstart line («check tombstones before proposing a resurrect»); architect light; result with a post-release click-check (delete an entry via MCP with reason, see the tombstone, list it, find it via search).

## Verification

- Conversion matrix tests (01), door/web tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear; 051 depends on this entity (parallel grooming, sequential landing).
- 2026-10-04 [agent] — validated with human proof: notification 4e6e087f-93fd-46a6-bfad-d6be4acf3643 replied 2026-10-04 12:00:44 UTC
</log>