---
feature: 124
slug: custom-scrollbars
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 124 — Custom scrollbars app-wide: styled thin scrollbar instead of the stock default

## Approach

One additive CSS block in `web/src/app/globals.css` inside the existing `@layer base`. No component changes: webkit pseudo-element rules are unqualified (cover every box that scrolls), Firefox properties sit on `:root` and inherit — so page scroll and every `overflow-y/x-auto` container, current and future, is styled without touching a single `.tsx`.

Selector/property contract (the shape to implement, final formatting is the coder's):

- `:root { scrollbar-width: thin; scrollbar-color: var(--color-muted) transparent; }` — Firefox; both properties inherit.
- `::-webkit-scrollbar { width: 8px; height: 8px; }` — unqualified, both axes (horizontal containers — mobile nav, code blocks — get `height: 8px` for free).
- `::-webkit-scrollbar-track`, `::-webkit-scrollbar-corner` — `background: transparent`.
- `::-webkit-scrollbar-thumb` — `background: var(--color-muted)`, fully rounded.
- `::-webkit-scrollbar-thumb:hover` — brightened via `color-mix(in oklab, var(--color-muted) 60%, var(--color-foreground))`, mirroring the existing `::selection` rule.

## Files

| File | Change |
|------|--------|
| `web/src/app/globals.css` | Additive block in `@layer base` (~15 lines). Existing content untouched. |
| `web/tests/scrollbar-styling.test.mts` | New guard test, house string-pin style: read `globals.css` via `readFileSync` (same pattern as `entry-tombstone.test.mts`), assert `scrollbar-width: thin`, `scrollbar-color`, `::-webkit-scrollbar` presence and no hardcoded hex inside the scrollbar block. |

Nothing else. Explicitly no changes to `badge.tsx` or any tone-class file (their `/70` class strings are pinned by source-reading tests).

## Pin strategy

- `globals.css` is read as a string by `web/tests/entry-tombstone.test.mts` ("tombstone rows carry their own color token" asserts `css.includes("--color-type-tombstone:")`). Edit additively — never reformat, reorder or "clean up" existing tokens.
- `/70` opacity class strings are pinned in `.tsx` source tests (e.g. `badge.tsx` `muted: "border border-border/50 text-muted/70"` in `entry-tombstone.test.mts:57`). Scrollbar work stays out of those files entirely.
- The new guard test pins the scrollbar block itself going forward, so a future theme-token refactor cannot silently drop it.

## Suggested task slice (post-validation breakdown)

1. **CSS block** (`web/src/app/globals.css`) — the selector contract above. Verify: lint/tsc/test/build green; dev-server eyeball of page, sidebar, modal, markdown pane, notification center in Chrome + Firefox.
2. **Guard test** (`web/tests/scrollbar-styling.test.mts`) — string pins. Verify: `npm test`.

Single-coder feature; slices are sequential in one task unless the dispatcher prefers two.

## Verification gates

- `cd web && npm run lint && npx tsc --noEmit`
- `npm test` (node --test — includes the new guard test and the existing `entry-tombstone` pin test)
- `npm run build` (static export must succeed)
- Manual pass: dev server, scroll the page + sidebar + a modal + a markdown pane + notification center in a Chromium browser and Firefox; thumb/hover visible, track invisible, no layout jump on scrollbar appear/disappear.

## Risks / notes

- Webkit thin scrollbars reserve 8px vs the stock ~15px — a one-time reflow at deploy, same reserved-gutter model as today (no new shift during use). `scrollbar-gutter` stays out per spec anti-scope.
- `color-mix` and `scrollbar-color` are baseline in evergreen browsers; engines without them keep stock scrollbars — the honest degrade the spec requires.
- Contrast math (spec AC): muted #8b8b96 on background #0a0a0f ≈ 5.9:1, on surface #14141c ≈ 5.4:1 — both above the 3:1 WCAG 1.4.11 non-text bar.

<log>
- 2026-10-06 [agent] — plan drafted; file-level approach verified against `globals.css`, the pin tests and the overflow/scroll sweep of `web/src`.
- 2026-10-06 [agent] — validated with human proof: notification 1a285e5d-d1da-4b55-a303-936e82e596c6 replied 2026-10-06 13:05:10 UTC
</log>