---
feature: 112
slug: approval-bubble-roomier
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Result 112 — Approval bubble: up to 800 chars of text, bigger window

## What shipped

- `bubbleText()` helper + `BUBBLE_TEXT_LIMIT = 800` (web/src/widgets/app-shell/lib/bubble-text.ts): text longer than the limit is sliced to 800 chars + «…», shorter text passes verbatim. Widget-internal lib, not exported from the widget's public API (FSD).
- `approval-bubble.tsx`: text renders through `bubbleText(item.text)`; `line-clamp-4` → `line-clamp-none` (the slice is the boundary now); width `w-72` → `sm:w-80` — one Tailwind step wider from `sm` up, mobile base untouched (spec tension «wider on one step» + «don't touch mobile» resolved with the `sm:` bump, interpretation logged on the task); `title={item.text}` retained so the full text stays available in the tooltip.
- Notification center untouched (full text lives there).

## Verification

- `npm run lint` — 0 errors (6 pre-existing warnings in untouched files); delta-only eslint pass clean.
- `npx tsc --noEmit` — clean.
- `npm test` — 103/103 (+4 new: slice+«…», short verbatim, exact-800 boundary).
- Reviewer re-ran all gates independently and confirmed 103/103.

## Review

Light review PASS-WITH-NITS, 0 blockers (report in conversation; pins in web/tests/approval-bubble.test.mts). Nits → microbatch ledger: #179 (UTF-16 slice can split a surrogate/ZWJ cluster at the 800 boundary — Intl.Segmenter grapheme mode as the 091-parity fix, cosmetic today since title + center hold the full text), #180 (dead assert in bubble-text.test.mts:16 checks the fixture literal instead of bubbleText output). Reviewer observation: the readFileSync source-pin pattern in approval-bubble.test.mts is the first of its kind in web/tests — justified (no render harness) but now exists as a precedent.

## Files

- web/src/widgets/app-shell/lib/bubble-text.ts (new)
- web/src/widgets/app-shell/ui/approval-bubble.tsx
- web/tests/bubble-text.test.mts (new)
- web/tests/approval-bubble.test.mts

<log>
- 2026-10-04 [agent] — coder drafted implementation; AC-1 ticked, result deferred to post-review per Simple path.
- 2026-10-04 [orchestrator] — light review PASS-WITH-NITS (0 blockers, nits #179/#180); result drafted; AC-2 ticked.
</log>