---
feature: 026
slug: entries-read-mode
status: validated
created: 2026-10-04
validated: 2026-10-05
---

# Result 026 — Entries read mode: inline expand/collapse + fullscreen view by title click

Web-only, task 01 (Simple path). Entries are now readable without opening the editor.

## Shipped

- Inline expand/collapse on the entries list: row click or chevron toggles the full body in place — same `Markdown` renderer as the edit preview, type icon/badge colors and tag chips stay in the header. Independent toggles per row (ReadonlySet state), chevron carries `aria-expanded` + `aria-controls`.
- Fullscreen read on title click: xl read-only modal (`EntryReadModal`, 014 task-read-mode pattern) — meta row (type badge, colored tag chips, `v·created·updated` line), full markdown body, Edit button that closes the read modal and opens the existing editor prefilled. Esc and backdrop close via the shared Modal.
- Row Edit/Delete buttons untouched; edit flow (editor, mutations, submit) unchanged — page wiring only added a `reading` state and the modal.

## Files

- `web/src/features/entries/lib/entry-read.ts` — new pure state/view-model lib: `toggleEntryExpanded`, `entryReadBody`, `entryReadMetaLine`, `entryTagChipStyle` (no runtime alias imports so node --test can load it).
- `web/src/features/entries/components/entry-read-modal.tsx` — new xl read modal.
- `web/src/features/entries/components/entry-list.tsx` — expansion state, chevron, row-click toggle, title-click read, tag chip style moved to the lib.
- `web/src/pages/entries-page/ui/entries-page.tsx` — read modal wiring, edit handoff from read.
- `web/src/features/entries/index.ts` — public API export.
- `web/tests/entry-read.test.mts` — 10 tests: expand/collapse independence and immutability, blank body, editor-prefill content identity, meta line, tag chip colors.

## Verification

- `npm test` 81/81 pass (10 new).
- `npm run lint` 0 errors (6 pre-existing warnings, none in touched files).
- `npx tsc --noEmit` clean.

<log>
- 2026-10-04 [agent] — drafted by task 01 execution; gates green, awaiting light review.
</log>
