---
feature: 051
slug: entries-materialization
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 051 — Entries materialization: notes/decisions/gates/guards на диск (+ tombstone-файлы, не молчаливый снос)

> Grooming 2026-10-04 (user in session). Hard coordination with 029 honored: 029 owns the tombstone ENTITY (its spec is being groomed in the same pass), 051 is its disk PROJECTION — the board stays the truth, files mirror it.

## Goal

Entries live only in the DB; the materializer never touches them. Decisions/guards are the contracts external reviewers and git history should see next to the code. This closes the gap and makes the hybrid complete.

## Decisions

- **One entry — one file:** `ENTRIES/{type}/{kebab(title)}-{id8}.md` under the project root (sibling of `BACKLOG/`; fixed location v1 — challengeable). Frontmatter: id, type, title, tags, createdAt, updatedAt; body is the markdown content.
- **Deletion on disk follows the board:** an entry's file is removed on the next pass (board = truth); with 029 landed, deletion CONVERTS to a tombstone entry — so the disk gains a tombstone FILE automatically, no marker-files at the old path (the «tombstone rather than delete» rule is served by the entity, not by a stub file). Pre-029 deploy order: 051's remove-path works standalone, tombstone files simply appear once 029 is in.
- **Clobber-guard (033) extends to ENTRIES/**: hand-edited tracked files refuse the pass; force warns per file. Same as BACKLOG today.
- **Rides the per-project materialization strategy** (off/on/archive — 006 contract); archive behaves per its existing rules.
- **One-way v1**: reverse import (disk → DB) is a non-goal; the file format (frontmatter + body) deliberately does not exclude it.

## Non-goals

- Planning tree (already materialized); reverse import v1; UI editor for materialized files; config for the ENTRIES path.

<log>
- 2026-10-04 [agent] — groomed with the user in session: ENTRIES/{type}/ layout, delete follows the board (029 conversion produces the tombstone file), clobber-guard extends, one-way v1.
- 2026-10-04 [agent] — validated with human proof: notification ff9b0055-ff9c-4468-9f6a-5f68686a40ba replied 2026-10-04 12:00:45 UTC
</log>