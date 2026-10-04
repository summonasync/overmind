---
feature: 113
slug: attention-row-polish
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Result 113 — Attention rows: dashboard number style + clickable hover-highlighted title

## What shipped

- `ceremonies-section.tsx`: the raw number span is now `FeatureNumberBadge` (065 shared primitive — the same one the board cards use; renders `#NNN` from the slug, dashboard parity). The title is a link to `feature.url` with the board-card hover treatment (`group` + `group-hover:text-primary` + the same focus-visible ring — byte-parity with the card title pattern). «Open →» untouched; awaiting rows untouched.
- `web/tests/attention-ceremony-rows.test.mts` (new): 3 source pins per the approval-bubble readFileSync precedent — badge usage (and absence of the raw span), linked title with hover, «Open →» preserved.

## Review

Light review PASS-WITH-NITS, 0 blockers (reviewer re-ran all gates independently). N1 (deep cross-feature import bypassing the barrel) fixed on the spot before the commit — import goes through `@/features/planning`, pin updated in pair. N2 → microbatch #184: `CeremonyFeature.number` (web type) no longer has readers — decide: drop the field or document as payload compatibility (wire contract stays, spec keeps payload out of scope).

## Verification

- npm run lint — 0 errors (6 pre-existing warnings in untouched files)
- npx tsc --noEmit — clean
- npm test — 106/106 (3 new pins; re-run green after the barrel fix)
- npm run build — green, /attention in static export
- Reviewer confirmed the badge/hover pattern is byte-parity with board cards and that plain `<a>` matches the app-wide navigation behavior (feature.url is absolute from the host; next/link unused in the repo).

## Files

- web/src/pages/attention-page/ui/ceremonies-section.tsx
- web/tests/attention-ceremony-rows.test.mts (new)

<log>
- 2026-10-04 [agent] — coder landed; gates green (106/106).
- 2026-10-04 [orchestrator] — light review PASS-WITH-NITS (0 blockers); N1 barrel fix landed pre-commit; N2 → microbatch #184; result drafted.
</log>