---
feature: 124
slug: custom-scrollbars
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 124 — Custom scrollbars app-wide: styled thin scrollbar instead of the stock default

## Outcome

Done. The app ships one thin, brand-consistent scrollbar everywhere: transparent track, muted thumb (#8b8b96 design-call #221 token) with a brighter hover, webkit pseudo-elements unqualified plus Firefox `scrollbar-width: thin` / `scrollbar-color` on `:root`. Single commit, two files, zero `.tsx` changes, zero JS.

- Commit: `a958703`
- Landed: 2026-10-06

## Changes

| File | Change |
|------|--------|
| `web/src/app/globals.css` | Additive `@layer base` block: `::-webkit-scrollbar` (8px both axes), transparent track/corner, `--color-muted` thumb, `color-mix(in oklab, ...)` hover; `:root` scrollbar-width/color for Firefox. Existing content untouched. |
| `web/tests/scrollbar-styling.test.mts` | New guard test — 3 string pins on `globals.css` (webkit block, `scrollbar-width: thin`, `scrollbar-color`) so a future token refactor cannot silently drop the styling. |

## Verification

- `npm run lint` — green.
- `npx tsc --noEmit` — green.
- `npm test` — green, 246 tests (incl. the 3 new pins and the pre-existing `entry-tombstone` pin on `globals.css`).
- `npm run build` — green (static export).

## Review

Light review — APPROVED, 0 blockers.

- Reviewer spot-check: 15/15 against the spec AC set (excluding AC7, see open item).
- Pin mutation-analysis: clean — pins fail when the block is mutated, pass as-is.
- Anti-scope verified: no `.tsx` diffs, no theme-token edits, no JS scroll handling, no new dependencies.

## Findings

- SUGGESTION filed to the improvement ledger as #247: the thumb background token is currently un-pinned (hover is pinned via `color-mix`, base thumb color is not asserted by the guard test).

## Open items (human door)

- AC7 — manual Chrome/Firefox eyeball pass: scrollbar look across page, sidebar, modals, markdown panes and the notification center; no layout jump on scrollbar appear/disappear. Stays with the human.

<refs>
- spec artifact c5da3ade-9e0e-4444-b88f-f49304ef1570 (validated)
- plan artifact 70b733be-7049-4da1-9309-1e9b8708dcb6 (validated)
- task 124/01 — 0437924d-84bc-4cd3-bc22-cd1c1eaa934b (done, light review approved)
- commit a958703; ledger suggestion #247
</refs>

<log>
- 2026-10-06 [agent] — result drafted from the executed task: commit a958703, gates green (lint/tsc/246 tests/build), light review approved 0 blockers, AC7 left to the human, #247 filed.
</log>