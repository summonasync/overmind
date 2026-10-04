---
feature: 029
slug: entry-tombstones
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 029 — Entry tombstones: тип записи + автозахват при удалении + список для агентов перед работой

> Grooming 2026-10-04 (user in session). User idea 2026-09-20; open questions resolved below (user leaned to tombstone-conversion — confirmed).

## Goal

Deletions stop being silent. A fifth entry type — `tombstone` — is the visible memory of what died and why; any agent lists tombstones before work the same way it reads notes today. DELETE converts instead of erasing.

## Decisions

- **EntryType.Tombstone** (fifth type, full citizen: filters, tones, tags).
- **DELETE /entries/{id} converts**: the original row (and its versions, cascade as today) goes; a tombstone entry is born carrying: original title (+ «(deleted)» marker), original type, deletion date, door+actor provenance, optional `reason` (new optional field on the delete request — MCP `delete_entry` gains it, human confirm dialog offers it), and the final body preserved inside a fenced block («what was here»). Version history is NOT carried over — the fenced final body is the memory; versions keep their current hard-delete fate.
- **Hand-made burials**: `write_entry type=tombstone` stays open to agents and humans (deliberate burial of an approach without deleting anything).
- **Discovery**: `list_entries type=tombstone` works by the existing type filter; tombstones ride the semantic/FTS indexes like all entries («what died» is searchable); onboarding quickstart gains one line: check tombstones before proposing anything that smells like a resurrect.
- **Web**: type in filters, muted/heavy tone for tombstone rows, create-доступен, delete of a tombstone allowed (rare cleanup, creates no second tombstone).

## Non-goals

- Restore UI (a tombstone carries enough to rebuild by hand); soft-delete rows (conversion wins — the type is the truth); bulk deletes; tombstone-on-top-of-tombstone chains.

<log>
- 2026-10-04 [agent] — groomed with the user in session: conversion confirmed (was the user's lean), versions not carried (final body fenced in), reason on delete, searchable, quickstart line.
- 2026-10-04 [agent] — validated with human proof: notification 4e6e087f-93fd-46a6-bfad-d6be4acf3643 replied 2026-10-04 12:00:44 UTC
</log>