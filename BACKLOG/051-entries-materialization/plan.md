---
feature: 051
slug: entries-materialization
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 051 — Entries materialization: notes/decisions/gates/guards на диск (+ tombstone-файлы, не молчаливый снос)

> Grooming 2026-10-04. 3 tasks, 01 → 02 → 03. Lands AFTER 029 (tombstone files need the entity; remove-path works standalone).

## Task chain

1. **01 — Materializer: ENTRIES write + remove paths.** One entry — one file `ENTRIES/{type}/{kebab(title)}-{id8}.md` (frontmatter id/type/title/tags/createdAt/updatedAt + body); strategy-respecting (off/on/archive); remove on the next pass when the entry is gone (board = truth); tombstone entries materialize like any entry (file appears on 029 conversion); tests: write shape, idempotent passes, remove path, tombstone file.
2. **02 — Clobber-guard + observability.** 033 guard extends to ENTRIES/ (hand-edited tracked files refuse, force warns per file); skipped/refused reporting rides the existing materialize report/status surfaces (074); tests: guard refusal, force pass, report lines.
3. **03 — Closeout.** Docs (structure + CONTEXT where the mirror is described); architect light; result with post-release click-check (create entry → file appears; hand-edit → refusal; delete entry with 029 → tombstone file appears, original file gone).

## Verification

- Write/remove tests (01), guard tests (02), gates each task, architect light (03).

<log>
- 2026-10-04 [agent] — plan groomed: 3 tasks linear, lands after 029; remove-path standalone-safe.
- 2026-10-04 [agent] — validated with human proof: notification ff9b0055-ff9c-4468-9f6a-5f68686a40ba replied 2026-10-04 12:00:45 UTC
</log>