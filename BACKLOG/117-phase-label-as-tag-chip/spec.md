---
feature: 117
slug: phase-label-as-tag-chip
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 117 — Board card: phase label as tag chip after the slug

> User dogfood feedback 2026-10-04: «статусы plan validated и тд — она прям между тайтлом и статусом. Я бы хотел её там где slug, после него — и как-то как наши тэги визуально».

## Goal

On the planning board card, the ceremony phase label («plan validated», «in progress», …) moves from its current spot in the right-side header group (between title and status select) to the slug line — rendered right after the slug, styled like the card's existing tag chips.

## Scope

- `web/src/features/planning/components/feature-board.tsx` — card header only: the slug line gains the phase chip; the plain-text phase span and its `Ceremony phase: …` tooltip are removed from the right-side group (blocked badge + status select stay as-is).
- `web/src/features/planning/lib/board-card-context.ts` — `cardPhaseLabel` becomes null-safe: null/empty/whitespace phase → null, otherwise the server label verbatim.
- `web/src/features/planning/types.ts` — `PlanningFeatureBoardItem.phase` widened to `string | null` (runtime honesty, mirrors `briefExcerpt`).
- `web/tests/board-card-context.test.mts` — pins extended for the null-safe mapping.

## Decisions

- **Reuse the shared `Badge` (`web/src/shared/ui/badge.tsx`) with the default `neutral` tone** — that IS the established tag chip (065): same border/background/muted text/`capitalize` as the version and spec/plan chips. No new chip variant, no local copy of the claim-chip `chipBase` span pattern.
- **No phase→color mapping** — the request prescribes none; the chip is neutral for every phase. A mapping can come later as its own groomed change.
- **Placement:** same line as the slug, inside the existing header link — slug keeps truncating (`min-w-0 truncate`), chip is `shrink-0`; the label text is the server label as-is (Badge `capitalize` applies, same as every other chip).
- **Tooltip dropped** — the chip text is self-describing, like every other chip on the card.
- **Null safety is web-side only:** the server contract (`CeremonyPhasePlanner.DeriveLabel`, 091) always returns one of the seven labels; the null guard is defensive web typing so a null/absent phase can never render an empty chip box or shift the header layout.

## Anti-scope

- Server: `CeremonyPhasePlanner`, planning contracts, mapper — the seven-label derivation is untouched.
- Phase label surfaces outside the board card (feature workspace, ceremony stepper, attention rows, queue payloads).
- Claim chip, artifact chips, priority select, status select — visual neighbors, not touched.
- Board filters, ordering, search — phase stays display-only here.

## Acceptance criteria

- [ ] Phase chip renders on the slug line, immediately after the slug text, inside the card header link, styled with the shared `Badge` neutral tone — visually consistent with the version/spec/plan chips.
- [ ] All seven phase values (drafting, writing spec, planning, plan validated, in progress, review, done) render through the chip.
- [ ] Null, absent or blank phase renders no chip at all — no empty chip box, no layout shift in the header row or slug line.
- [ ] The old plain-text phase span and its `Ceremony phase: …` tooltip are gone from the right-side header group; blocked badge and status select keep their order.
- [ ] Long slug still truncates on one line with the chip visible; slug and chip never wrap.
- [ ] `npm run lint`, `npx tsc --noEmit`, `npm test`, `npm run build` green in `web/`; `board-card-context` tests cover the null/empty/whitespace → null mapping.

## Refs

- 091-board-card-brief-phase-status — introduces the derived phase label on the queue payload (`phase` field); derivation stays server-side.
- 065-feature-number-badge-on-card — the shared `Badge` primitive being reused.

## Obstacles

- None at groom time. Web has no component-render test harness (node:test pure-logic only), so chip placement is pinned by tsc/build plus a visual tap during validation.

<log>
- 2026-10-06 [agent] — groomed from user dogfood feedback 2026-10-04: Badge neutral chip after the slug, tooltip dropped, null-safe label, no phase→color map (none prescribed).
- 2026-10-06 [agent] — validated with human proof: notification d557b412-83dc-43f2-801e-9e34d8cac869 replied 2026-10-06 13:05:11 UTC
</log>